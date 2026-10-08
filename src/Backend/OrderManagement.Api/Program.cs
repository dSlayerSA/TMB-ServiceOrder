using Microsoft.EntityFrameworkCore;
using OrderManagement.Api.Hubs;
using OrderManagement.Api.Messaging;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Orders;
using OrderManagement.Infrastructure;
using OrderManagement.Infrastructure.Messaging;
using OrderManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderStatusNotifier, SignalROrderStatusNotifier>();
builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<OrderStatusChangedRelayConsumer>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres")
    .AddRabbitMQ(
        sp => sp.GetRequiredService<RabbitMqConnectionFactory>().GetConnectionAsync().GetAwaiter().GetResult(),
        name: "rabbitmq");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderManagementDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<OrdersHub>("/hubs/orders");
app.MapHealthChecks("/health");

app.Run();

// Exposes the top-level Program for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
