using OrderManagement.Infrastructure;
using OrderManagement.Infrastructure.Messaging;
using OrderManagement.Infrastructure.Observability;
using OrderManagement.Worker.Consumers;

// The Worker has no HTTP surface of its own, but it is hosted as a minimal web
// app so the orchestrator (Docker Compose) can probe /health and know whether
// this process can actually reach Postgres and RabbitMQ - a process that is
// "up" but unable to consume is not healthy.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, Telemetry.ServiceNameWorker);
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<StuckOrdersSweeper>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres")
    .AddRabbitMQ(
        sp => sp.GetRequiredService<RabbitMqConnectionFactory>().GetConnectionAsync().GetAwaiter().GetResult(),
        name: "rabbitmq");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
