using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderManagement.Application.Analytics;
using OrderManagement.Domain.Orders;

namespace OrderManagement.Infrastructure.Analytics;

/// <summary>One tool the model may call, described in a provider-neutral way.</summary>
public record AnalyticsTool(
    string Name,
    string Description,
    IReadOnlyDictionary<string, JsonElement> Properties);

public record ToolOutcome(string Content, bool IsError);

/// <summary>
/// Everything about the analytics tools that does not depend on which model is
/// answering: what the tools are, what the model is told, and how a call is
/// executed against the database.
///
/// The provider adapters only translate this into their own wire format and
/// drive their own loop, so adding a provider never duplicates a tool schema
/// or a parsing rule.
/// </summary>
public class OrderAnalyticsToolset
{
    private readonly IOrderAnalyticsQueries _queries;
    private readonly ILogger _logger;

    public OrderAnalyticsToolset(IOrderAnalyticsQueries queries, ILogger logger)
    {
        _queries = queries;
        _logger = logger;
    }

    public static IReadOnlyList<AnalyticsTool> Definitions { get; } =
    [
        new("count_orders",
            "Count orders, optionally filtered by status and creation date range.",
            new Dictionary<string, JsonElement>
            {
                ["status"] = StatusSchema(),
                ["created_from"] = CreatedFromSchema(),
                ["created_to"] = CreatedToSchema(),
            }),
        new("sum_order_amounts",
            "Total monetary value (and matching order count) of orders, optionally filtered by status and creation date range.",
            new Dictionary<string, JsonElement>
            {
                ["status"] = StatusSchema(),
                ["created_from"] = CreatedFromSchema(),
                ["created_to"] = CreatedToSchema(),
            }),
        new("orders_by_status",
            "Count and total value of orders grouped by status. Use this to compare statuses in a single call.",
            new Dictionary<string, JsonElement>
            {
                ["created_from"] = CreatedFromSchema(),
                ["created_to"] = CreatedToSchema(),
            }),
        new("processing_time_seconds",
            "Average, fastest and slowest time in seconds from order creation to Finalizado, over finalized orders. Use this for questions about how long orders take.",
            new Dictionary<string, JsonElement>
            {
                ["created_from"] = CreatedFromSchema(),
                ["created_to"] = CreatedToSchema(),
            }),
        new("list_orders",
            "Most recent individual orders, including customer and product. Use this for questions about specific orders, who bought what, or which products were sold. Returns at most 50.",
            new Dictionary<string, JsonElement>
            {
                ["status"] = StatusSchema(),
                ["created_from"] = CreatedFromSchema(),
                ["created_to"] = CreatedToSchema(),
                ["limit"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    description = "How many orders to return (1-50, default 10).",
                }),
            }),
    ];

    /// <summary>
    /// Dates are spelled out explicitly because small local models are much
    /// weaker at date arithmetic than a frontier model: giving them the computed
    /// boundaries removes the step they most often get wrong.
    /// </summary>
    public static string BuildSystemPrompt()
    {
        var now = DateTime.UtcNow;
        var today = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        string Iso(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        return string.Join(
            "\n",
            "Você é um assistente de analytics de um sistema de gestão de pedidos.",
            "Responda a perguntas sobre os pedidos reais que estão no banco de dados.",
            "",
            "Contexto do domínio:",
            "- Cada pedido tem cliente, produto, valor, status e data de criação.",
            "- O caminho normal é Pendente -> Processando -> Finalizado, sempre nessa ordem.",
            "- Um pedido também pode terminar em Falhou (o worker não conseguiu concluir)",
            "  ou Cancelado (desistiu antes de concluir). Os dois são estados finais.",
            "- \"Tempo de processamento\" (ou de aprovação) é o tempo entre a criação do",
            "  pedido e o momento em que ele chegou a Finalizado. Pedidos que falharam",
            "  ou foram cancelados não entram nessa média.",
            "- Os valores estão em reais (BRL).",
            "",
            "Datas já calculadas, use exatamente estes valores:",
            $"- agora: {Iso(now)}",
            $"- início de hoje: {Iso(today)}",
            $"- início de amanhã: {Iso(today.AddDays(1))}",
            $"- início desta semana (7 dias atrás): {Iso(today.AddDays(-7))}",
            $"- início deste mês: {Iso(monthStart)}",
            $"- início do mês que vem: {Iso(monthStart.AddMonths(1))}",
            "",
            "Para \"hoje\" use created_from = início de hoje e created_to = início de amanhã.",
            "Para \"este mês\" use created_from = início deste mês e created_to = início do mês que vem.",
            "",
            "Regras:",
            "- Sempre obtenha os números através das ferramentas. Nunca estime, suponha",
            "  ou invente dados.",
            "- Nunca anuncie o que vai fazer. Não escreva \"vou usar a ferramenta X\"",
            "  nem descreva um plano: chame a ferramenta direto. Só escreva texto",
            "  depois de já ter os dados em mãos.",
            "- Se as ferramentas disponíveis não permitirem responder, diga isso",
            "  claramente em vez de adivinhar.",
            "- Responda em português do Brasil, de forma amigável e direta, citando os",
            "  números concretos que você obteve. Não descreva as ferramentas usadas.",
            "- Responda SOMENTE o que foi perguntado, em no máximo 3 frases. Não ofereça",
            "  sugestões, análises extras nem próximos passos.");
    }

    /// <summary>Runs one tool call and serializes the result for the model.</summary>
    public async Task<ToolOutcome> RunAsync(string name, JsonElement input, CancellationToken ct)
    {
        try
        {
            var filter = ParseFilter(input);

            object payload = name switch
            {
                "count_orders" => new { count = await _queries.CountAsync(filter, ct) },
                "sum_order_amounts" => await _queries.SumAmountsAsync(filter, ct),
                "orders_by_status" => new { statuses = await _queries.BreakdownByStatusAsync(filter, ct) },
                "processing_time_seconds" => await _queries.ProcessingTimeAsync(filter, ct),
                "list_orders" => new
                {
                    orders = await _queries.ListOrdersAsync(filter, GetInt(input, "limit") ?? 10, ct),
                },
                _ => throw new InvalidOperationException($"Unknown tool: {name}"),
            };

            return new ToolOutcome(JsonSerializer.Serialize(payload), IsError: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Analytics tool {Tool} failed.", name);
            return new ToolOutcome($"The tool failed: {ex.Message}", IsError: true);
        }
    }

    private static OrderAnalyticsFilter ParseFilter(JsonElement input) => new(
        ParseStatus(GetString(input, "status")),
        ParseUtc(GetString(input, "created_from")),
        ParseUtc(GetString(input, "created_to")));

    private static OrderStatus? ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var status))
            return status;

        throw new ArgumentException(
            $"Unknown status: {value}. Valid values are Pendente, Processando, Finalizado, Falhou, Cancelado.");
    }

    /// <summary>
    /// Accepts either a date or a full instant and always lands on UTC - Npgsql
    /// rejects a non-UTC DateTime for a timestamptz column.
    /// </summary>
    private static DateTime? ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            throw new ArgumentException($"Not a valid ISO-8601 date or timestamp: {value}");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    private static string? GetString(JsonElement input, string property) =>
        input.ValueKind == JsonValueKind.Object
        && input.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement input, string property)
    {
        if (input.ValueKind != JsonValueKind.Object || !input.TryGetProperty(property, out var value))
            return null;

        // Small models often send numbers as strings, so both are accepted.
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetInt32(),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static JsonElement StatusSchema() => JsonSerializer.SerializeToElement(new
    {
        type = "string",
        @enum = new[] { "Pendente", "Processando", "Finalizado", "Falhou", "Cancelado" },
        description = "Filter to a single order status.",
    });

    private static JsonElement CreatedFromSchema() => JsonSerializer.SerializeToElement(new
    {
        type = "string",
        description = "Inclusive lower bound on the order creation date, ISO-8601 UTC (e.g. 2026-10-13T00:00:00Z).",
    });

    private static JsonElement CreatedToSchema() => JsonSerializer.SerializeToElement(new
    {
        type = "string",
        description = "Exclusive upper bound on the order creation date, ISO-8601 UTC. For a whole month, pass the first day of the next month.",
    });
}
