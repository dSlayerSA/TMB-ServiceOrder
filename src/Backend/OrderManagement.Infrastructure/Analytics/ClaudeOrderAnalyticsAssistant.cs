using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Analytics;
using OrderManagement.Infrastructure.Observability;

namespace OrderManagement.Infrastructure.Analytics;

/// <summary>
/// Answers analytics questions with the Claude API, letting the model choose
/// which aggregate to run through <see cref="OrderAnalyticsToolset"/>.
///
/// The model never sees or writes SQL: each tool maps to one parameterized
/// query, so the worst a crafted question can do is ask for a number it was
/// already allowed to ask for.
/// </summary>
public class ClaudeOrderAnalyticsAssistant : IOrderAnalyticsAssistant
{
    private readonly OrderAnalyticsToolset _toolset;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<ClaudeOrderAnalyticsAssistant> _logger;
    private readonly AnthropicClient _client;

    public ClaudeOrderAnalyticsAssistant(
        OrderAnalyticsToolset toolset,
        AnalyticsOptions options,
        ILogger<ClaudeOrderAnalyticsAssistant> logger)
    {
        _toolset = toolset;
        _options = options;
        _logger = logger;
        _client = new AnthropicClient { ApiKey = options.ApiKey };
    }

    public bool IsEnabled => true;

    public string Provider => $"Claude ({_options.Model})";

    public async Task<AnalyticsAnswer> AskAsync(string question, CancellationToken ct = default)
    {
        using var activity = Telemetry.Source.StartActivity("analytics question");
        activity?.SetTag("analytics.provider", "claude");

        var messages = new List<MessageParam>
        {
            new() { Role = Role.User, Content = question },
        };
        var toolsUsed = new List<string>();

        for (var iteration = 0; iteration < _options.MaxToolIterations; iteration++)
        {
            var response = await _client.Messages.Create(
                new MessageCreateParams
                {
                    Model = _options.Model,
                    MaxTokens = 4096,
                    System = OrderAnalyticsToolset.BuildSystemPrompt(),
                    Tools = BuildTools(),
                    Messages = messages,
                },
                cancellationToken: ct);

            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];
            var answer = new StringBuilder();

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                    answer.Append(text.Text);
                }
                else if (block.TryPickThinking(out ThinkingBlock? thinking))
                {
                    // The signature must survive the round trip or the API rejects it.
                    assistantContent.Add(new ThinkingBlockParam
                    {
                        Thinking = thinking.Thinking,
                        Signature = thinking.Signature,
                    });
                }
                else if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
                {
                    assistantContent.Add(new RedactedThinkingBlockParam { Data = redacted.Data });
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });

                    toolsUsed.Add(toolUse.Name);
                    var outcome = await _toolset.RunAsync(
                        toolUse.Name, JsonSerializer.SerializeToElement(toolUse.Input), ct);

                    // Every tool_use block needs a matching tool_result, errors included.
                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = outcome.Content,
                        IsError = outcome.IsError,
                    });
                }
            }

            if (toolResults.Count == 0)
            {
                activity?.SetTag("analytics.iterations", iteration + 1);
                activity?.SetTag("analytics.tools_used", string.Join(",", toolsUsed));
                return new AnalyticsAnswer(answer.ToString().Trim(), toolsUsed);
            }

            messages.Add(new() { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new() { Role = Role.User, Content = toolResults });
        }

        _logger.LogWarning(
            "Analytics question hit the {Max}-iteration tool limit without a final answer.",
            _options.MaxToolIterations);

        throw new InvalidOperationException(
            "The assistant could not reach an answer within the allowed number of steps.");
    }

    private static IReadOnlyList<ToolUnion> BuildTools() =>
        OrderAnalyticsToolset.Definitions
            .Select(tool => (ToolUnion)new Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = new()
                {
                    Properties = tool.Properties.ToDictionary(p => p.Key, p => p.Value),
                },
            })
            .ToList();
}
