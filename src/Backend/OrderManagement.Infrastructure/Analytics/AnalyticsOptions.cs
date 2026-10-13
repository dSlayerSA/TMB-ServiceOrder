namespace OrderManagement.Infrastructure.Analytics;

public enum AnalyticsProvider
{
    /// <summary>Anthropic Claude API. Needs a key; best quality, costs per question.</summary>
    Claude = 0,

    /// <summary>A model served locally by Ollama. Free to run, no account, lower quality.</summary>
    Ollama = 1,

    /// <summary>Module switched off.</summary>
    None = 2,
}

public class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>
    /// Which backend answers questions. Explicit rather than auto-detected: a
    /// silent fallback to a provider that is not running turns a clear
    /// "not configured" message into a confusing connection error.
    /// </summary>
    public AnalyticsProvider Provider { get; set; } = AnalyticsProvider.Claude;

    /// <summary>
    /// Anthropic API key. Kept out of source: set it via the ANTHROPIC_API_KEY
    /// environment variable (or Analytics:ApiKey). Without it the Claude
    /// provider stays switched off instead of failing at startup.
    /// </summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5";

    public string OllamaBaseUrl { get; set; } = "http://ollama:11434";

    public string OllamaModel { get; set; } = "qwen2.5:3b";

    /// <summary>Local models are slow, so the HTTP timeout is generous.</summary>
    public int OllamaTimeoutSeconds { get; set; } = 180;

    /// <summary>Safety stop for the tool-use loop, so a question can never spin forever.</summary>
    public int MaxToolIterations { get; set; } = 6;

    public bool IsEnabled => Provider switch
    {
        AnalyticsProvider.Claude => !string.IsNullOrWhiteSpace(ApiKey),
        AnalyticsProvider.Ollama => !string.IsNullOrWhiteSpace(OllamaBaseUrl),
        _ => false,
    };
}
