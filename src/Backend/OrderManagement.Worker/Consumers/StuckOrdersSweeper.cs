using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Domain.Orders;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Worker.Consumers;

/// <summary>
/// Safety net for the Pendente->Processando->Finalizado(5s) flow: if the Worker
/// crashes/restarts during the 5s delay in <see cref="OrderCreatedConsumer"/>,
/// the in-memory Task.Delay is lost. This sweeper periodically finalizes any
/// order that has been sitting in Processando for longer than the 5s window,
/// without requiring a message broker plugin (e.g. delayed-message-exchange).
/// </summary>
public class StuckOrdersSweeper : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StuckOrdersSweeper> _logger;

    public StuckOrdersSweeper(IServiceScopeFactory scopeFactory, ILogger<StuckOrdersSweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Failed to sweep stuck orders.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
        var statusPublisher = scope.ServiceProvider.GetRequiredService<IOrderStatusEventPublisher>();

        var cutoff = DateTime.UtcNow - ProcessingTimeout;
        var stuckOrders = await db.Orders
            .Include(o => o.StatusHistories)
            .Where(o => o.Status == OrderStatus.Processando && o.UpdatedAt <= cutoff)
            .ToListAsync(ct);

        foreach (var order in stuckOrders)
        {
            var newHistory = order.TransitionTo(OrderStatus.Finalizado, "Pedido finalizado pelo sweeper (recuperação).");
            if (newHistory is null)
                continue;

            db.Entry(newHistory).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
            await statusPublisher.PublishAsync(new OrderStatusChangedEvent(order.Id, order.Status, order.UpdatedAt), ct);
            _logger.LogInformation("Sweeper finalized order {OrderId} stuck in Processando.", order.Id);
        }
    }
}
