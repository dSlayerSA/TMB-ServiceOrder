using Microsoft.AspNetCore.SignalR;
using OrderManagement.Application.Abstractions;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Api.Hubs;

public class SignalROrderStatusNotifier : IOrderStatusNotifier
{
    private readonly IHubContext<OrdersHub> _hubContext;

    public SignalROrderStatusNotifier(IHubContext<OrdersHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyStatusChangedAsync(Guid orderId, OrderStatus status, CancellationToken ct = default) =>
        _hubContext.Clients.All.SendAsync(
            "OrderStatusChanged",
            new { orderId, status = status.ToString() },
            ct);
}
