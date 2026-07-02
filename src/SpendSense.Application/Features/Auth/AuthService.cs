using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpendSense.Application.Abstractions;
using SpendSense.Application.Options;
using SpendSense.Domain.Entities;

namespace SpendSense.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class AuthService(IAppDbContext db, IPasswordService passwords, ITokenService tokens, IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = passwords.Hash(request.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!passwords.Verify(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashToken(request.RefreshToken);
        var refreshToken = await db.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid refresh token.");

        if (!refreshToken.IsActive || refreshToken.User is null)
        {
            throw new UnauthorizedAccessException("Refresh token is expired or revoked.");
        }

        refreshToken.RevokedOnUtc = DateTime.UtcNow;
        return await IssueTokensAsync(refreshToken.User, cancellationToken, refreshToken);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var hash = tokens.HashToken(request.RefreshToken);
        var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (refreshToken is not null)
        {
            refreshToken.RevokedOnUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstAsync(x => x.Id == userId, cancellationToken);
        return new UserProfileResponse(user.Id, user.Email, user.FullName, user.EmailVerified);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken, RefreshToken? rotatedToken = null)
    {
        var refresh = tokens.CreateRefreshToken();
        var refreshEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokens.HashToken(refresh),
            ExpiresOnUtc = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)
        };
        if (rotatedToken is not null)
        {
            rotatedToken.ReplacedByTokenHash = refreshEntity.TokenHash;
        }
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(user.Id, user.Email, user.FullName, tokens.CreateAccessToken(user), refresh, DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes));
    }
}
