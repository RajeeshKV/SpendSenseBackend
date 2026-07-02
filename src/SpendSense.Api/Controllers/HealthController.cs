using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendSense.Infrastructure.Persistence;
using SpendSense.Shared;
using System.Diagnostics;

namespace SpendSense.Api.Controllers;

public sealed record HealthComponent(string Name, string Status, long DurationMs, string? Error = null);
public sealed record HealthResponse(string Status, DateTime CheckedOnUtc, IReadOnlyList<HealthComponent> Components);

[ApiController]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthResponse>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<HealthResponse>>> Get(CancellationToken cancellationToken)
    {
        var health = await CheckHealthAsync(cancellationToken);
        var isHealthy = health.Status == "Healthy";
        var response = isHealthy
            ? ApiResponse<HealthResponse>.Ok(health, "Backend and database are healthy.", HttpContext.TraceIdentifier)
            : ApiResponse<HealthResponse>.Fail("Backend or database health check failed.", health.Components.Where(x => x.Error is not null).Select(x => $"{x.Name}: {x.Error}"), HttpContext.TraceIdentifier) with { Data = health };

        return StatusCode(isHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpHead]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Head(CancellationToken cancellationToken)
    {
        var health = await CheckHealthAsync(cancellationToken);
        return StatusCode(health.Status == "Healthy" ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
    }

    private async Task<HealthResponse> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var components = new List<HealthComponent>
        {
            new("Backend", "Healthy", 0)
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();
            components.Add(new HealthComponent("Database", canConnect ? "Healthy" : "Unhealthy", stopwatch.ElapsedMilliseconds, canConnect ? null : "Database connection failed."));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            components.Add(new HealthComponent("Database", "Unhealthy", stopwatch.ElapsedMilliseconds, ex.Message));
        }

        var status = components.All(x => x.Status == "Healthy") ? "Healthy" : "Unhealthy";
        return new HealthResponse(status, DateTime.UtcNow, components);
    }
}
