using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Auth;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/profile")]
public sealed class ProfileController(IAuthService auth) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> Get(CancellationToken cancellationToken) => Envelope(await auth.GetProfileAsync(UserId, cancellationToken));
}
