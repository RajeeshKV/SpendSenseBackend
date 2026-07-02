using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Entities;
using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Ai;

public interface IAiInsightService
{
    Task<AiInsightResponse> AnalyzeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AiInsightResponse?> LatestAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AiInsightService(IAppDbContext db, IAiService ai) : IAiInsightService
{
    public async Task<AiInsightResponse> AnalyzeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var summary = await ai.GenerateInsightsAsync(userId, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = new DateOnly(today.Year, today.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var insight = new Insight { UserId = userId, Period = InsightPeriod.Monthly, PeriodStart = start, PeriodEnd = end, Provider = "Configured", Summary = summary, PayloadJson = "{}" };
        db.Insights.Add(insight);
        await db.SaveChangesAsync(cancellationToken);
        return new AiInsightResponse(insight.Summary, insight.PeriodStart, insight.PeriodEnd, insight.Provider);
    }

    public async Task<AiInsightResponse?> LatestAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Insights.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedOnUtc)
            .Select(x => new AiInsightResponse(x.Summary, x.PeriodStart, x.PeriodEnd, x.Provider))
            .FirstOrDefaultAsync(cancellationToken);
}
