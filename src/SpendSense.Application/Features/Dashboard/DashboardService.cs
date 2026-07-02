using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategorySpend>> CategoriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlySpend>> MonthlyAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MerchantSpend>> MerchantsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class DashboardService(IAppDbContext db) : IDashboardService
{
    public async Task<DashboardSummary> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var categories = await CategoriesAsync(userId, cancellationToken);
        var monthly = await MonthlyAsync(userId, cancellationToken);
        var merchants = await MerchantsAsync(userId, cancellationToken);
        var spend = await db.Transactions.Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Debit).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var income = await db.Transactions.Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Credit).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        return new DashboardSummary(spend, income, income - spend, categories.FirstOrDefault()?.Category, categories, monthly, merchants);
    }

    public async Task<IReadOnlyList<CategorySpend>> CategoriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var totals = db.Transactions
            .Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Debit)
            .GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, Amount = x.Sum(t => t.Amount) });

        return await (from total in totals
                join category in db.Categories on total.CategoryId equals (Guid?)category.Id into categories
                from category in categories.DefaultIfEmpty()
                select new CategorySpend(
                    total.CategoryId,
                    category == null ? "Uncategorized" : category.Name,
                    total.Amount))
            .OrderByDescending(x => x.Amount)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonthlySpend>> MonthlyAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Transactions.Where(x => x.UserId == userId)
            .GroupBy(x => new { x.Date.Year, x.Date.Month })
            .Select(x => new MonthlySpend(x.Key.Year, x.Key.Month, x.Where(t => t.DebitCredit == DebitCredit.Debit).Sum(t => t.Amount), x.Where(t => t.DebitCredit == DebitCredit.Credit).Sum(t => t.Amount)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MerchantSpend>> MerchantsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Transactions.Where(x => x.UserId == userId && x.DebitCredit == DebitCredit.Debit)
            .GroupBy(x => x.Merchant)
            .Select(x => new MerchantSpend(x.Key, x.Sum(t => t.Amount)))
            .OrderByDescending(x => x.Amount)
            .Take(20)
            .ToListAsync(cancellationToken);
}
