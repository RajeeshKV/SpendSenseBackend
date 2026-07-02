using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Domain.Enums;

namespace SpendSense.Infrastructure.Ai;

public sealed class ConfiguredAiService(IAppDbContext db, IOptions<AiOptions> options) : IAiService
{
    public async Task<string> GenerateInsightsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var totals = await db.Transactions
            .Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Debit)
            .GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, Amount = x.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync(cancellationToken);

        var categoryIds = totals
            .Where(x => x.CategoryId.HasValue)
            .Select(x => x.CategoryId!.Value)
            .Distinct()
            .ToList();
        var categories = await db.Categories
            .Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var categoryTotals = totals
            .Select(x => new
            {
                Category = x.CategoryId.HasValue && categories.TryGetValue(x.CategoryId.Value, out var category) ? category : "Uncategorized",
                x.Amount
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            var top = categoryTotals.FirstOrDefault();
            return top is null
                ? "No spending data is available yet. Upload a statement to generate insights."
                : $"Top spending category is {top.Category} at {top.Amount:0.00}. Configure AI__ApiKey to enable provider-generated insights.";
        }

        return $"AI provider {options.Value.Provider} is configured. HTTP provider call can be enabled here using only aggregated category, merchant, and monthly totals.";
    }

    public Task<string> AnalyzeTransactionsAsync(Guid userId, CancellationToken cancellationToken = default) => GenerateInsightsAsync(userId, cancellationToken);
    public Task<string> ChatAsync(Guid userId, string prompt, CancellationToken cancellationToken = default) => Task.FromResult("AI chat is available after configuring an AI provider implementation.");
}
