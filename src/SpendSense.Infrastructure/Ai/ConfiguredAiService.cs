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
        var totals = db.Transactions
            .Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Debit)
            .GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, Amount = x.Sum(t => t.Amount) });

        var categoryTotals = await (from total in totals
                join category in db.Categories on total.CategoryId equals (Guid?)category.Id into categories
                from category in categories.DefaultIfEmpty()
                select new
                {
                    Category = category == null ? "Uncategorized" : category.Name,
                    total.Amount
                })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .ToListAsync(cancellationToken);

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
