using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Analytics;
using OrderManagement.Infrastructure.Observability;

namespace OrderManagement.Infrastructure.Analytics;

/// <summary>
/// Answers analytics questions with a model running locally under Ollama, using
/// the same tools as the Claude adapter.
///
/// The point of this adapter is that the module costs nothing to run and needs
/// no account: everything above <see cref="IOrderAnalyticsAssistant"/> - the
/// endpoint, the UI, the tools, the queries - is untouched by the swap.
/// </summary>
public class OllamaOrderAnalyticsAssistant : IOrderAnalyticsAssistant
{
    public const string HttpClientName = "ollama";

    private readonly OrderAnalyticsToolset _toolset;
    private readonly AnalyticsOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaOrderAnalyticsAssistant> _logger;

    public OllamaOrderAnalyticsAssistant(
        OrderAnalyticsToolset toolset,
        AnalyticsOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaOrderAnalyticsAssistant> logger)
    {
        _toolset = toolset;
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsEnabled => true;

    public string Provider => $"Ollama ({_options.OllamaModel})";

    public async Task<AnalyticsAnswer> AskAsync(string question, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("analytics question");
        activity?.SetTag("analytics.provider", "ollama");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        var messages = new JsonArray
        {
            Message("system", OrderAnalyticsToolset.BuildSystemPrompt()),
            Message("user", question),
        };
        var toolsUsed = new List<string>();

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var request = new JsonObject
            {
                ["model"] = _options.OllamaModel,
                ["stream"] = false,
                ["messages"] = messages.DeepClone(),
                ["tools"] = BuildTools(),
                // Deterministic: this is a data lookup, not a creative task.
                ["options"] = new JsonObject { ["temperature"] = 0 },
            };

            var response = await client.PostAsJsonAsync("/api/chat", request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<JsonNode>(ct)
                ?? throw new InvalidOperationException("Ollama returned an empty response.");

            var assistant = body["message"]
                ?? throw new InvalidOperationException("Ollama response had no message.");

            // Echoed back verbatim so the model keeps its own tool_calls in context.
            messages.Add(assistant.DeepClone());

            var calls = assistant["tool_calls"]?.AsArray();
            if (calls is null || calls.Count == 0)
            {
                var answer = assistant["content"]?.GetValue<string>()?.Trim() ?? string.Empty;
                activity?.SetTag("analytics.iterations", iteration + 1);
                activity?.SetTag("analytics.tools_used", string.Join(",", toolsUsed));
                return new AnalyticsAnswer(answer, toolsUsed);
            }

            foreach (var call in calls)
            {
                var function = call?["function"];
                var name = function?["name"]?.GetValue<string>() ?? string.Empty;
                toolsUsed.Add(name);

                var outcome = await _toolset.RunAsync(name, ParseArguments(function?["arguments"]), ct);
                // Ollama tool messages carry no call id - results are matched by order.
                messages.Add(Message("tool", outcome.Content));
            }
        }

        _logger.LogWarning(
            "Analytics question hit the {Max}-iteration tool limit without a final answer.",
            _options.MaxToolIterations);

        throw new InvalidOperationException(
            "The assistant could not reach an answer within the allowed number of steps.");
    }

    /// <summary>
    /// Arguments usually arrive as an object, but smaller models sometimes send
    /// them as a JSON string. Both are accepted rather than failing the call.
    /// </summary>
    private static JsonElement ParseArguments(JsonNode? arguments)
    {
        if (arguments is null)
            return JsonDocument.Parse("{}").RootElement;

        if (arguments is JsonValue value && value.TryGetValue<string>(out var raw))
        {
            try
            {
                return JsonDocument.Parse(raw).RootElement;
            }
            catch (JsonException)
            {
                return JsonDocument.Parse("{}").RootElement;
            }
        }

        return JsonSerializer.Deserialize<JsonElement>(arguments.ToJsonString());
    }

    private static JsonObject Message(string role, string content) =>
        new() { ["role"] = role, ["content"] = content };

    private static JsonArray BuildTools()
    {
        var tools = new JsonArray();

        foreach (var tool in OrderAnalyticsToolset.Definitions)
        {
            var properties = new JsonObject();
            foreach (var (name, schema) in tool.Properties)
                properties[name] = JsonNode.Parse(schema.GetRawText());

            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                    },
                },
            });
        }

        return tools;
    }
}
