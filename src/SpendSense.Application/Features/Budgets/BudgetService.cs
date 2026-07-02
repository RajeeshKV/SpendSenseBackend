using Microsoft.EntityFrameworkCore;
using SpendSense.Application.Abstractions;
using SpendSense.Domain.Entities;

namespace SpendSense.Application.Features.Budgets;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BudgetResponse> CreateAsync(Guid userId, BudgetRequest request, CancellationToken cancellationToken = default);
    Task<BudgetResponse> UpdateAsync(Guid userId, Guid id, BudgetRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}

public sealed class BudgetService(IAppDbContext db) : IBudgetService
{
    public async Task<IReadOnlyList<BudgetResponse>> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Budgets.Include(x => x.Category).Where(x => x.UserId == userId && !x.IsDeleted)
            .Select(x => new BudgetResponse(x.Id, x.CategoryId, x.Category == null ? string.Empty : x.Category.Name, x.Name, x.Amount, x.Period, x.StartsOn, x.EndsOn, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<BudgetResponse> CreateAsync(Guid userId, BudgetRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstAsync(x => x.Id == request.CategoryId, cancellationToken);
        var budget = new Budget { UserId = userId, CategoryId = category.Id, Name = request.Name, Amount = request.Amount, Period = request.Period, StartsOn = request.StartsOn, EndsOn = request.EndsOn, IsActive = request.IsActive };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(cancellationToken);
        return new BudgetResponse(budget.Id, budget.CategoryId, category.Name, budget.Name, budget.Amount, budget.Period, budget.StartsOn, budget.EndsOn, budget.IsActive);
    }

    public async Task<BudgetResponse> UpdateAsync(Guid userId, Guid id, BudgetRequest request, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.Include(x => x.Category).FirstAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        budget.CategoryId = request.CategoryId; budget.Name = request.Name; budget.Amount = request.Amount; budget.Period = request.Period; budget.StartsOn = request.StartsOn; budget.EndsOn = request.EndsOn; budget.IsActive = request.IsActive; budget.ModifiedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var category = await db.Categories.FirstAsync(x => x.Id == budget.CategoryId, cancellationToken);
        return new BudgetResponse(budget.Id, budget.CategoryId, category.Name, budget.Name, budget.Amount, budget.Period, budget.StartsOn, budget.EndsOn, budget.IsActive);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var budget = await db.Budgets.FirstAsync(x => x.UserId == userId && x.Id == id, cancellationToken);
        budget.IsDeleted = true;
        budget.ModifiedOnUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
