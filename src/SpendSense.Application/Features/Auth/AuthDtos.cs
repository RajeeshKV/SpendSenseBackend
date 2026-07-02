using FluentValidation;

namespace SpendSense.Application.Features.Auth;

public sealed record RegisterRequest(string Email, string FullName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record VerifyEmailRequest(string Email, string Token);
public sealed record AuthResponse(Guid UserId, string Email, string FullName, string AccessToken, string RefreshToken, DateTime AccessTokenExpiresOnUtc);
public sealed record UserProfileResponse(Guid UserId, string Email, string FullName, bool EmailVerified);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(128);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
