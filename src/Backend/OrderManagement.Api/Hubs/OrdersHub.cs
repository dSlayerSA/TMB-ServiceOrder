using Microsoft.AspNetCore.SignalR;

namespace OrderManagement.Api.Hubs;

/// <summary>
/// Push-only hub: clients just listen for "OrderStatusChanged" events.
/// No client-invokable methods are needed for this challenge's scope.
/// </summary>
public class OrdersHub : Hub
{
}
