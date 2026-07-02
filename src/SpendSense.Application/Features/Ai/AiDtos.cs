namespace SpendSense.Application.Features.Ai;

public sealed record AiAnalyzeRequest(string? Focus = null);
public sealed record AiInsightResponse(string Summary, DateOnly PeriodStart, DateOnly PeriodEnd, string Provider);
