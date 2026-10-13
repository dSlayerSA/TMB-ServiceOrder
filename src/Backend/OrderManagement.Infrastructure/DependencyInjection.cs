using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderManagement.Application.Abstractions;
using OrderManagement.Application.Analytics;
using OrderManagement.Application.Catalog;
using OrderManagement.Infrastructure.Messaging;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence (EF Core + Postgres) and messaging (RabbitMQ) services
    /// shared by both the Api and the Worker processes.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<OrderManagementDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductCatalog, ProductCatalog>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrderManagementDbContext>());

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddScoped<IOrderStatusEventPublisher, RabbitMqOrderStatusEventPublisher>();

        return services;
    }
}
