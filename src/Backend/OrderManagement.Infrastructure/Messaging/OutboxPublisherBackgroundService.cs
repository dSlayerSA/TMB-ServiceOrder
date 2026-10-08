using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderManagement.Domain.Outbox;
using OrderManagement.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace OrderManagement.Infrastructure.Messaging;

/// <summary>
/// Polls the OutboxMessages table and publishes pending messages to RabbitMQ,
/// marking each as processed only after the broker has confirmed receipt.
/// This is the "relay" half of the Outbox Pattern: the write side (OrderService)
/// never talks to RabbitMQ directly, so a DB commit and a broker publish can
/// never partially succeed.
/// </summary>
public class OutboxPublisherBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnectionFactory connectionFactory,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to publish pending outbox messages.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var channel = await _connectionFactory.CreateChannelAsync(enablePublisherConfirms: true, ct: ct);
        await RabbitMqTopology.DeclareOrderCreatedTopologyAsync(channel, ct);

        foreach (var message in pending)
        {
            await PublishOneAsync(channel, message, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task PublishOneAsync(IChannel channel, OutboxMessage message, CancellationToken ct)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(message.Payload);
            var properties = new BasicProperties
            {
                MessageId = message.Id.ToString(),
                CorrelationId = message.CorrelationId.ToString(),
                ContentType = "application/json",
                Persistent = true,
                Headers = new Dictionary<string, object?>
                {
                    ["EventType"] = message.EventType
                }
            };

            await channel.BasicPublishAsync(
                exchange: RabbitMqTopology.OrdersExchange,
                routingKey: RabbitMqTopology.OrderCreatedRoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: ct);

            message.MarkProcessed();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish outbox message {MessageId} (attempt {Attempts}).", message.Id, message.Attempts + 1);
            message.MarkFailed(ex.Message);
        }
    }
}
