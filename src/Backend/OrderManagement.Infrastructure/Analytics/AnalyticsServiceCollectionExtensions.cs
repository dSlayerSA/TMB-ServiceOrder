using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Analytics;
using OrderManagement.Infrastructure.Persistence;

namespace OrderManagement.Infrastructure.Analytics;

public static class AnalyticsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the natural-language analytics module. Only the Api needs it, so
    /// it is deliberately not part of AddInfrastructure (the Worker would end up
    /// holding a client it never uses).
    ///
    /// The provider is chosen once, here: everything above
    /// <see cref="IOrderAnalyticsAssistant"/> is identical either way.
    /// </summary>
    public static IServiceCollection AddAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new AnalyticsOptions();
        configuration.GetSection(AnalyticsOptions.SectionName).Bind(options);

        // ANTHROPIC_API_KEY is the conventional name for this credential, so it
        // is accepted directly rather than forcing an Analytics__ApiKey alias.
        options.ApiKey = string.IsNullOrWhiteSpace(options.ApiKey)
            ? configuration["ANTHROPIC_API_KEY"]
            : options.ApiKey;

        services.AddSingleton(options);
        services.AddScoped<IOrderAnalyticsQueries, OrderAnalyticsQueries>();
        services.AddScoped<OrderAnalyticsToolset>(sp => new OrderAnalyticsToolset(
            sp.GetRequiredService<IOrderAnalyticsQueries>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<OrderAnalyticsToolset>()));

        if (options.Provider == AnalyticsProvider.Ollama && options.IsEnabled)
        {
            services.AddHttpClient(OllamaOrderAnalyticsAssistant.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(options.OllamaBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.OllamaTimeoutSeconds);
            });
            services.AddScoped<IOrderAnalyticsAssistant, OllamaOrderAnalyticsAssistant>();
        }
        else if (options.Provider == AnalyticsProvider.Claude && options.IsEnabled)
        {
            services.AddScoped<IOrderAnalyticsAssistant, ClaudeOrderAnalyticsAssistant>();
        }
        else
        {
            services.AddScoped<IOrderAnalyticsAssistant, DisabledOrderAnalyticsAssistant>();
        }

        return services;
    }
}
