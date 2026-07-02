using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Ai;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/ai")]
public sealed class AiController(IAiInsightService insights) : ApiControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<ApiResponse<AiInsightResponse>>> Analyze(AiAnalyzeRequest request, CancellationToken cancellationToken) => Envelope(await insights.AnalyzeAsync(UserId, cancellationToken), "AI analysis generated.");

    [HttpGet("latest")]
    public async Task<ActionResult<ApiResponse<AiInsightResponse?>>> Latest(CancellationToken cancellationToken)
    {
        AiInsightResponse? latest = await insights.LatestAsync(UserId, cancellationToken);
        return Envelope<AiInsightResponse?>(latest);
    }
}
