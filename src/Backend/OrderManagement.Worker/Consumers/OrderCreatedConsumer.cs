using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Messaging;
using OrderManagement.Domain.Orders;
using OpenTelemetry;
using OrderManagement.Infrastructure.Messaging;
using OrderManagement.Infrastructure.Observability;
using OrderManagement.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderManagement.Worker.Consumers;

/// <summary>
/// Consumes "OrderCreated" events and drives the order through
/// Pendente -> Processando -> (5s later) Finalizado.
///
/// Idempotency has two layers, matching two different failure modes:
///  1. Broker-level: RabbitMQ is at-least-once, so the same message can be
///     redelivered (e.g. after a crash before ack). The ProcessedMessages
///     table short-circuits on the message's own id.
///  2. Domain-level: Order.TransitionTo() is itself a no-op if the order is
///     already at (or past) the target status, so even a message we've
///     never seen before can't push an order backwards or re-apply a step.
/// </summary>
public class OrderCreatedConsumer : BackgroundService
{
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        RabbitMqConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderCreatedConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connectionFactory.CreateChannelAsync(ct: stoppingToken);
        await RabbitMqTopology.DeclareOrderCreatedTopologyAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(
            queue: RabbitMqTopology.OrderCreatedQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        // The publisher wrote the trace context into the AMQP headers; picking it
        // up here is what keeps the Worker's work on the caller's trace instead
        // of starting an unrelated one.
        var parentContext = Telemetry.ExtractTraceContext(
            new Telemetry.AmqpHeaderCarrier(ea.BasicProperties.Headers));
        Baggage.Current = parentContext.Baggage;

        using var activity = Telemetry.Source.StartActivity(
            $"consume {RabbitMqTopology.OrderCreatedQueue}",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "receive");
        activity?.SetTag("messaging.source.name", RabbitMqTopology.OrderCreatedQueue);

        try
        {
            var messageId = Guid.TryParse(ea.BasicProperties.MessageId, out var id) ? id : Guid.Empty;
            var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(Encoding.UTF8.GetString(ea.Body.ToArray()));
            activity?.SetTag("order.id", @event?.OrderId.ToString());

            if (@event is null)
            {
                _logger.LogWarning("Received an OrderCreated message with no readable payload; discarding.");
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IOrderStatusEventPublisher>();

            if (messageId != Guid.Empty && await db.ProcessedMessages.AnyAsync(m => m.MessageId == messageId, stoppingToken))
            {
                _logger.LogInformation("Message {MessageId} already processed, skipping (idempotent).", messageId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            var order = await db.Orders.Include(o => o.StatusHistories).FirstOrDefaultAsync(o => o.Id == @event.OrderId, stoppingToken);
            if (order is null)
            {
                _logger.LogWarning("Order {OrderId} not found for OrderCreated event; discarding.", @event.OrderId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            // The order may have been cancelled while this message sat in the
            // queue. Terminal is terminal: there is nothing to do, and trying to
            // move it would throw and send the message round the queue forever.
            if (order.IsTerminal)
            {
                _logger.LogInformation(
                    "Order {OrderId} is already {Status}; nothing to process.", order.Id, order.Status);

                if (messageId != Guid.Empty)
                    db.ProcessedMessages.Add(new ProcessedMessage(messageId));

                await db.SaveChangesAsync(stoppingToken);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
                return;
            }

            var newHistory = order.TransitionTo(OrderStatus.Processando, "Processamento iniciado pelo worker.");
            if (newHistory is not null)
                db.Entry(newHistory).State = EntityState.Added;

            if (messageId != Guid.Empty)
                db.ProcessedMessages.Add(new ProcessedMessage(messageId));

            await db.SaveChangesAsync(stoppingToken);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);

            if (newHistory is not null)
                await statusPublisher.PublishAsync(new OrderStatusChangedEvent(order.Id, order.Status, order.UpdatedAt), stoppingToken);

            // Fire-and-forget delayed finalization; the StuckOrdersSweeper is the
            // safety net if the Worker crashes/restarts during this delay.
            _ = Task.Run(
                () => ApplyPlannedEndingAsync(
                    order.Id, order.Plan.DurationSeconds, activity?.Context ?? default, stoppingToken),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(ex, "Failed to handle OrderCreated message; requeueing.");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, stoppingToken);
        }
    }

    /// <summary>
    /// Waits out the order's processing window and then applies the ending its
    /// plan asked for - Finalizado, Falhou or Cancelado.
    /// </summary>
    private async Task ApplyPlannedEndingAsync(
        Guid orderId,
        int durationSeconds,
        ActivityContext parentContext,
        CancellationToken appStoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds), appStoppingToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        // Explicitly parented: this runs on a detached task long after the consume
        // span ended, so there is no ambient context left to inherit.
        using var activity = Telemetry.Source.StartActivity(
            "apply planned ending", ActivityKind.Internal, parentContext);
        activity?.SetTag("order.id", orderId.ToString());

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
        var statusPublisher = scope.ServiceProvider.GetRequiredService<IOrderStatusEventPublisher>();

        var order = await db.Orders.Include(o => o.StatusHistories).FirstOrDefaultAsync(o => o.Id == orderId, appStoppingToken);
        if (order is null)
            return;

        // Cancelled while we were waiting out the plan. This runs on a detached
        // task, so an exception here would go unobserved - the guard is the only
        // thing standing between a cancellation and a silent crash.
        if (order.IsTerminal)
        {
            activity?.SetTag("order.status", order.Status.ToString());
            return;
        }

        var ending = order.Plan.FinalStatus;
        activity?.SetTag("order.status", ending.ToString());

        var newHistory = order.TransitionTo(ending, $"{StuckOrdersSweeper.NoteFor(ending)}.");
        if (newHistory is null)
            return;

        db.Entry(newHistory).State = EntityState.Added;
        await db.SaveChangesAsync(appStoppingToken);
        await statusPublisher.PublishAsync(new OrderStatusChangedEvent(order.Id, order.Status, order.UpdatedAt), appStoppingToken);
    }
}
