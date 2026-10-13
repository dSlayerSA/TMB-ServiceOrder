using OrderManagement.Application.Analytics;

namespace OrderManagement.Infrastructure.Analytics;

/// <summary>
/// Stands in when no provider is configured, so the rest of the system never
/// has to reason about a half-built assistant.
/// </summary>
public class DisabledOrderAnalyticsAssistant : IOrderAnalyticsAssistant
{
    public bool IsEnabled => false;

    public string Provider => "Desativado";

    public Task<AnalyticsAnswer> AskAsync(string question, CancellationToken ct = default) =>
        throw new AnalyticsNotConfiguredException();
}
