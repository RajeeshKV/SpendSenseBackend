using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpendSense.Application.Features.Auth;
using SpendSense.Shared;

namespace SpendSense.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request, CancellationToken cancellationToken) => Envelope(await auth.RegisterAsync(request, cancellationToken), "Registered successfully.");

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken cancellationToken) => Envelope(await auth.LoginAsync(request, cancellationToken), "Logged in successfully.");

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshRequest request, CancellationToken cancellationToken) => Envelope(await auth.RefreshAsync(request, cancellationToken), "Token refreshed.");

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout(RefreshRequest request, CancellationToken cancellationToken) { await auth.LogoutAsync(request, cancellationToken); return EmptyEnvelope("Logged out."); }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public ActionResult<ApiResponse<object>> VerifyEmail(VerifyEmailRequest request) => EmptyEnvelope("Email verification endpoint is reserved for Brevo template flow.");

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public ActionResult<ApiResponse<object>> ForgotPassword(ForgotPasswordRequest request) => EmptyEnvelope("Password reset email flow is reserved for Brevo template flow.");

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public ActionResult<ApiResponse<object>> ResetPassword(ResetPasswordRequest request) => EmptyEnvelope("Password reset endpoint is reserved for token flow implementation.");
}
