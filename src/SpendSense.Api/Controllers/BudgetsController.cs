using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Budgets;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/budgets")]
public sealed class BudgetsController(IBudgetService budgets) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BudgetResponse>>>> Get(CancellationToken cancellationToken) => Envelope(await budgets.GetAsync(UserId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<BudgetResponse>>> Create(BudgetRequest request, CancellationToken cancellationToken) => Envelope(await budgets.CreateAsync(UserId, request, cancellationToken), "Budget created.");

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BudgetResponse>>> Update(Guid id, BudgetRequest request, CancellationToken cancellationToken) => Envelope(await budgets.UpdateAsync(UserId, id, request, cancellationToken), "Budget updated.");

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken) { await budgets.DeleteAsync(UserId, id, cancellationToken); return EmptyEnvelope("Budget deleted."); }
}
