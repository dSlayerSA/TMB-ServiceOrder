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
/// Safety net for the delay in <see cref="OrderCreatedConsumer"/>: that wait
/// lives in memory, so a Worker restart mid-flight would otherwise strand the
/// order in Processando forever. This sweeper picks up anything whose own
/// processing window has elapsed and applies the ending its plan asked for,
/// without needing a broker plugin (e.g. delayed-message-exchange).
/// </summary>
public class StuckOrdersSweeper : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Headroom past the order's own duration, so the sweeper never races the
    /// consumer that is still legitimately waiting on it.
    /// </summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

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

        var now = DateTime.UtcNow;
        var candidates = await db.Orders
            .Include(o => o.StatusHistories)
            .Where(o => o.Status == OrderStatus.Processando)
            .ToListAsync(ct);

        // Each order carries its own duration, so the cutoff is per-order and is
        // applied in memory rather than as a single SQL predicate.
        var overdue = candidates.Where(o =>
            o.UpdatedAt.Add(TimeSpan.FromSeconds(o.Plan.DurationSeconds)).Add(Grace) <= now);

        foreach (var order in overdue)
        {
            var newHistory = order.TransitionTo(
                order.Plan.FinalStatus,
                $"{NoteFor(order.Plan.FinalStatus)} (recuperação pelo sweeper).");
            if (newHistory is null)
                continue;

            db.Entry(newHistory).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
            await statusPublisher.PublishAsync(new OrderStatusChangedEvent(order.Id, order.Status, order.UpdatedAt), ct);
            _logger.LogInformation(
                "Sweeper moved order {OrderId} stuck in Processando to {Status}.", order.Id, order.Status);
        }
    }

    internal static string NoteFor(OrderStatus status) => status switch
    {
        OrderStatus.Falhou => "Processamento falhou",
        OrderStatus.Cancelado => "Pedido cancelado após tentativas do worker",
        _ => "Pedido finalizado pelo worker"
    };
}
