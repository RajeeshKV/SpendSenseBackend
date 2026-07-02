using Microsoft.AspNetCore.Mvc;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/settings")]
public sealed class SettingsController : ApiControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<object>> Get() => Envelope<object>(new { emailReports = true, budgetAlerts = true });
}
