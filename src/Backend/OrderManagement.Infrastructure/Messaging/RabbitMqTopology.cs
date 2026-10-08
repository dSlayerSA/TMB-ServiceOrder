using RabbitMQ.Client;

namespace OrderManagement.Infrastructure.Messaging;

/// <summary>
/// Central definition of exchanges/queues so publishers and consumers always
/// declare (idempotently) the same topology, regardless of which one starts first.
/// </summary>
public static class RabbitMqTopology
{
    public const string OrdersExchange = "orders.exchange";
    public const string OrderCreatedRoutingKey = "order.created";
    public const string OrderCreatedQueue = "orders.created.queue";

    public const string OrdersDeadLetterExchange = "orders.dlx";
    public const string OrderCreatedDeadLetterQueue = "orders.created.queue.dlq";

    public const string OrderStatusExchange = "orders.status.exchange";
    public const string OrderStatusChangedRoutingKey = "order.status-changed";
    public const string OrderStatusChangedQueue = "orders.status-changed.queue";

    public static async Task DeclareOrderCreatedTopologyAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(OrdersExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(OrdersDeadLetterExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        await channel.QueueDeclareAsync(OrderCreatedDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(OrderCreatedDeadLetterQueue, OrdersDeadLetterExchange, OrderCreatedRoutingKey, cancellationToken: ct);

        var args = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = OrdersDeadLetterExchange,
            ["x-dead-letter-routing-key"] = OrderCreatedRoutingKey
        };
        await channel.QueueDeclareAsync(OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false, arguments: args, cancellationToken: ct);
        await channel.QueueBindAsync(OrderCreatedQueue, OrdersExchange, OrderCreatedRoutingKey, cancellationToken: ct);
    }

    public static async Task DeclareOrderStatusChangedTopologyAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(OrderStatusExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(OrderStatusChangedQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(OrderStatusChangedQueue, OrderStatusExchange, OrderStatusChangedRoutingKey, cancellationToken: ct);
    }
}
