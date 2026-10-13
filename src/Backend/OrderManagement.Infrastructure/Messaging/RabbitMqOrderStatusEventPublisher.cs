using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Infrastructure.Observability;
using RabbitMQ.Client;

namespace OrderManagement.Infrastructure.Messaging;

public class RabbitMqOrderStatusEventPublisher : IOrderStatusEventPublisher
{
    private readonly RabbitMqConnectionFactory _connectionFactory;

    public RabbitMqOrderStatusEventPublisher(RabbitMqConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PublishAsync(OrderStatusChangedEvent @event, CancellationToken ct = default)
    {
        var channel = await _connectionFactory.CreateChannelAsync(ct: ct);
        await RabbitMqTopology.DeclareOrderStatusChangedTopologyAsync(channel, ct);

        // Started inside the Worker's consume span, so the status update stays on
        // the same trace that began with the original POST /orders.
        using var activity = Telemetry.Source.StartActivity(
            $"publish {RabbitMqTopology.OrderStatusChangedRoutingKey}",
            ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.operation.name", "publish");
        activity?.SetTag("messaging.destination.name", RabbitMqTopology.OrderStatusExchange);
        activity?.SetTag("order.id", @event.OrderId.ToString());
        activity?.SetTag("order.status", @event.Status.ToString());

        var headers = new Dictionary<string, object?>();
        Telemetry.InjectTraceContext(activity, headers);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        var properties = new BasicProperties
        {
            CorrelationId = @event.OrderId.ToString(),
            ContentType = "application/json",
            Persistent = true,
            Headers = headers
        };

        await channel.BasicPublishAsync(
            exchange: RabbitMqTopology.OrderStatusExchange,
            routingKey: RabbitMqTopology.OrderStatusChangedRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }
}
