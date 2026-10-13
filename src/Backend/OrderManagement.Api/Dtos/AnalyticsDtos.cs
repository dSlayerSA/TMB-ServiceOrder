using System.ComponentModel.DataAnnotations;

namespace OrderManagement.Api.Dtos;

public record AskAnalyticsRequest([Required, MaxLength(500)] string Question);

public record AnalyticsAnswerResponse(string Answer, IReadOnlyList<string> ToolsUsed);
