using System.Security.Claims;
using SpendSense.Application.Abstractions;

namespace SpendSense.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public string? Email => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email) ?? accessor.HttpContext?.User.FindFirstValue("email");
}
