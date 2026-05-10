using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Sessions;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SessionOptions = LicenceBackend.Infrastructure.Options.SessionOptions;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("sessions")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class SessionsController(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ISessionTokenIssuer sessionIssuer,
    ISessionRefreshTokenRepository refreshTokens,
    ILoginRateLimiter loginRateLimiter,
    TimeProvider time,
    IOptions<SessionOptions> sessionOptions
) : ControllerBase
{
    private readonly SessionOptions _sessionOptions = sessionOptions.Value;

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var ip = (HttpContext.Connection.RemoteIpAddress ?? IPAddress.None).ToString();
            var email = request.Email.ToLowerInvariant();
            var decision = await loginRateLimiter.TryAcquireAsync(ip, email, cancellationToken);
            if (!decision.Acquired) return RateLimitRejection.AsResult(HttpContext, decision.RetryAfter);
        }

        var user = await users.FindByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            passwordHasher.VerifyDummy(request.Password);
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "invalid_credentials",
                detail: "Email or password is incorrect."
            );
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "invalid_credentials",
                detail: "Email or password is incorrect."
            );

        if (user.Status == UserStatus.Suspended)
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "account_suspended",
                detail: "This account is suspended. Contact an administrator."
            );

        var issued = await IssueSessionPairAsync(user, cancellationToken);
        return Ok(ToResponse(issued, user));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimiterPolicyNames.Refresh)]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh(
        [FromBody] string refreshToken,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return InvalidRefresh();

        byte[] hash;
        try
        {
            hash = RefreshTokenHasher.Hash(refreshToken);
        }
        catch (ArgumentException)
        {
            return InvalidRefresh();
        }

        var existing = await refreshTokens.FindByHashAsync(hash, cancellationToken);
        if (existing is null) return InvalidRefresh();

        var now = time.GetUtcNow();
        if (existing.ExpiresAt <= now) return InvalidRefresh();

        if (existing.RevokedAt is not null)
        {
            if (existing.ReplacedBy is not null) await refreshTokens.RevokeAllForUserAsync(existing.UserId, cancellationToken);
            return InvalidRefresh();
        }

        var user = await users.FindByIdAsync(existing.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Suspended) return InvalidRefresh();

        var newRefresh = BuildRefreshToken(user.Id, now, out var rawRefresh);
        var rotated = await refreshTokens.RotateAsync(existing.Id, newRefresh, cancellationToken);
        if (!rotated)
        {
            await refreshTokens.RevokeAllForUserAsync(existing.UserId, cancellationToken);
            return InvalidRefresh();
        }

        var session = sessionIssuer.Issue(user, newRefresh.Id);
        return Ok(new SessionResponse(
                      session.Token,
                      session.ExpiresAt,
                      rawRefresh,
                      newRefresh.ExpiresAt,
                      ToUserResponse(user))
        );
    }

    [HttpDelete]
    [Authorize]
    [EnableRateLimiting(RateLimiterPolicyNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (TryGetSessionId(out var sessionId)) await refreshTokens.RevokeByIdAsync(sessionId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("all")]
    [Authorize]
    [EnableRateLimiting(RateLimiterPolicyNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        await refreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        return NoContent();
    }

    private async Task<IssuedPair> IssueSessionPairAsync(User user, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var refresh = BuildRefreshToken(user.Id, now, out var rawRefresh);
        await refreshTokens.CreateAsync(refresh, cancellationToken);

        var session = sessionIssuer.Issue(user, refresh.Id);
        return new IssuedPair(session, rawRefresh, refresh.ExpiresAt);
    }

    private SessionRefreshToken BuildRefreshToken(Guid userId, DateTimeOffset now, out string rawToken)
    {
        rawToken = RefreshTokenGenerator.Generate();
        var hash = RefreshTokenHasher.Hash(rawToken);
        return new SessionRefreshToken(
            Guid.NewGuid(),
            userId,
            hash,
            now,
            now.AddSeconds(_sessionOptions.RefreshTtlSeconds),
            null,
            null
        );
    }

    private static SessionResponse ToResponse(IssuedPair pair, User user)
    {
        return new SessionResponse(
            pair.Session.Token,
            pair.Session.ExpiresAt,
            pair.RawRefresh,
            pair.RefreshExpiresAt,
            ToUserResponse(user)
        );
    }

    private ObjectResult InvalidRefresh()
    {
        return Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "invalid_refresh",
            detail: "The refresh token is not valid."
        );
    }

    private bool TryGetSessionId(out Guid sessionId)
    {
        var sidClaim = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sidClaim, out sessionId);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }

    private static UserResponse ToUserResponse(User user)
    {
        return new UserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString().ToLowerInvariant(),
            user.Status.ToString().ToLowerInvariant(),
            user.CreatedAt
        );
    }

    private sealed record IssuedPair(
        SessionToken Session,
        string RawRefresh,
        DateTimeOffset RefreshExpiresAt
    );
}
