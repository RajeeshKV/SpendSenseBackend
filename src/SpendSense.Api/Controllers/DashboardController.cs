using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Dashboard;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardSummary>>> Get(CancellationToken cancellationToken) => Envelope(await dashboard.GetAsync(UserId, cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategorySpend>>>> Categories(CancellationToken cancellationToken) => Envelope(await dashboard.CategoriesAsync(UserId, cancellationToken));

    [HttpGet("monthly")]
    [HttpGet("trends")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MonthlySpend>>>> Monthly(CancellationToken cancellationToken) => Envelope(await dashboard.MonthlyAsync(UserId, cancellationToken));

    [HttpGet("merchants")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MerchantSpend>>>> Merchants(CancellationToken cancellationToken) => Envelope(await dashboard.MerchantsAsync(UserId, cancellationToken));
}
