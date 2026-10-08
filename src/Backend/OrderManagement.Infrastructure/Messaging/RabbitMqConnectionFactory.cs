using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderManagement.Infrastructure.Messaging;

/// <summary>
/// Lazily creates and reuses a single AMQP connection per process, handing out
/// a fresh channel per caller (channels are cheap, connections are not).
/// </summary>
public class RabbitMqConnectionFactory : IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.User,
                Password = _options.Password
            };

            _connection = await factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync(bool enablePublisherConfirms = false, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        if (!enablePublisherConfirms)
            return await connection.CreateChannelAsync(cancellationToken: ct);

        var options = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        return await connection.CreateChannelAsync(options, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
