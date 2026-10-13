using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderManagement.Tests.Integration;

/// <summary>
/// Spins up a real, disposable PostgreSQL instance via Testcontainers so the
/// integration tests exercise actual EF Core + Npgsql behaviour (migrations,
/// transactions) rather than an in-memory provider. The RabbitMQ-dependent
/// hosted services are removed: this factory is only used to verify the
/// write side (Order + OutboxMessage persisted together), not the broker.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync() => await _postgres.StopAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // These MUST go through UseSetting rather than ConfigureAppConfiguration:
        // under minimal hosting (WebApplication.CreateBuilder), the app re-applies
        // its own appsettings.json when the inner host is built, which silently
        // overrides anything added via ConfigureAppConfiguration here. The symptom
        // is nasty - the tests fall back to appsettings' Host=localhost;Port=5432
        // and quietly pass against whatever Postgres the developer happens to have
        // running locally, instead of the disposable container.
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());

        // Point at a host that will never resolve, so the outbox publisher and
        // the status-changed relay fail fast instead of accidentally reaching
        // a real broker (e.g. one left running from `docker compose up`) - this
        // test only verifies the write side (Order + OutboxMessage persisted).
        builder.UseSetting("RabbitMq:Host", "rabbitmq.invalid");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            // Belt-and-suspenders: even if a hosted service above isn't removed for
            // some reason, don't let its connection failure take the test host down.
            services.Configure<HostOptions>(o => o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
        });
    }
}
