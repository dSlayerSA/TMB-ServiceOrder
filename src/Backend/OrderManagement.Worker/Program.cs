using OrderManagement.Infrastructure;
using OrderManagement.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OrderCreatedConsumer>();
builder.Services.AddHostedService<StuckOrdersSweeper>();

var host = builder.Build();
host.Run();
