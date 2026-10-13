using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderManagement.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Wires up distributed tracing for a host. Spans are exported over OTLP when
    /// "Otlp:Endpoint" is configured (the Docker Compose stack points it at Jaeger);
    /// otherwise they go to the console, so tracing is still observable when running
    /// the projects directly with `dotnet run`.
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        var otlpEndpoint = configuration["Otlp:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new BackgroundPollSampler())
                    .AddSource(Telemetry.Source.Name)
                    // Npgsql emits its own spans, so every DB round trip shows up
                    // without an extra instrumentation package.
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation(options =>
                        // The container healthchecks poll /health every 10s; tracing
                        // them would bury the actual order flow.
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation();

                if (string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddConsoleExporter();
                else
                    tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
