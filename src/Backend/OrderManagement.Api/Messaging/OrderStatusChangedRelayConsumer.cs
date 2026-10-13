using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OpenTelemetry;
using OrderManagement.Infrastructure.Messaging;
using OrderManagement.Infrastructure.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderManagement.Api.Messaging;

/// <summary>
/// Relays "order status changed" events published by the Worker (a separate
/// process) into this process's SignalR hub, so connected browsers get pushed
/// updates without the Worker needing any knowledge of SignalR.
/// </summary>
public class OrderStatusChangedRelayConsumer : BackgroundService
{
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderStatusChangedRelayConsumer> _logger;

    public OrderStatusChangedRelayConsumer(
        RabbitMqConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<OrderStatusChangedRelayConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connectionFactory.CreateChannelAsync(ct: stoppingToken);
        await RabbitMqTopology.DeclareOrderStatusChangedTopologyAsync(channel, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var parentContext = Telemetry.ExtractTraceContext(
                new Telemetry.AmqpHeaderCarrier(ea.BasicProperties.Headers));
            Baggage.Current = parentContext.Baggage;

            // Closes the loop: the SignalR push lands on the same trace as the
            // POST /orders that originally created the order.
            using var activity = Telemetry.Source.StartActivity(
                $"consume {RabbitMqTopology.OrderStatusChangedQueue}",
                ActivityKind.Consumer,
                parentContext.ActivityContext);

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.operation.name", "receive");
            activity?.SetTag("messaging.source.name", RabbitMqTopology.OrderStatusChangedQueue);

            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var @event = JsonSerializer.Deserialize<OrderStatusChangedEvent>(json);
                if (@event is not null)
                {
                    activity?.SetTag("order.id", @event.OrderId.ToString());
                    activity?.SetTag("order.status", @event.Status.ToString());

                    using var scope = _scopeFactory.CreateScope();
                    var notifier = scope.ServiceProvider.GetRequiredService<IOrderStatusNotifier>();
                    await notifier.NotifyStatusChangedAsync(@event.OrderId, @event.Status, stoppingToken);
                }

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Failed to relay order status change to SignalR.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(
            queue: RabbitMqTopology.OrderStatusChangedQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }
}
