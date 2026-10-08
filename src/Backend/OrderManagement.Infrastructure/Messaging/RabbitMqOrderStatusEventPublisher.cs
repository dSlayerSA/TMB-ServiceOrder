using System.Text;
using System.Text.Json;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
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

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        var properties = new BasicProperties
        {
            CorrelationId = @event.OrderId.ToString(),
            ContentType = "application/json",
            Persistent = true
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
