using FluentValidation;
using SpendSense.Domain.Enums;

namespace SpendSense.Application.Features.Budgets;

public sealed record BudgetRequest(Guid CategoryId, string Name, decimal Amount, BudgetPeriod Period, DateOnly StartsOn, DateOnly? EndsOn, bool IsActive = true);
public sealed record BudgetResponse(Guid Id, Guid CategoryId, string CategoryName, string Name, decimal Amount, BudgetPeriod Period, DateOnly StartsOn, DateOnly? EndsOn, bool IsActive);

public sealed class BudgetRequestValidator : AbstractValidator<BudgetRequest>
{
    public BudgetRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}
