using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId => Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException("User id claim is missing."));
    protected ActionResult<ApiResponse<T>> Envelope<T>(T? data, string message = "Success") => Ok(ApiResponse<T>.Ok(data, message, HttpContext.TraceIdentifier));
    protected ActionResult<ApiResponse<object>> EmptyEnvelope(string message = "Success") => Ok(ApiResponse<object>.Ok(null, message, HttpContext.TraceIdentifier));
}
