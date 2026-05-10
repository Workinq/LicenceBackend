using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("users")]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class UsersController(
    IUserRepository users,
    IUserStatusHistoryRepository userStatusHistory,
    ILicenceRepository licences,
    ILicenceVerificationAttemptRepository verificationAttempts,
    IProductRepository products,
    IPasswordHasher passwordHasher,
    TimeProvider time
) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        if (await users.ExistsByEmailAsync(request.Email, cancellationToken))
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "email_already_exists",
                detail: $"A user with email '{request.Email}' already exists."
            );

        var role = Enum.Parse<UserRole>(request.Role, true);
        var now = time.GetUtcNow();
        var user = new User(
            Guid.NewGuid(),
            request.Email.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            passwordHasher.Hash(request.Password),
            string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            role,
            UserStatus.Active,
            now,
            now
        );

        await users.CreateAsync(user, cancellationToken);

        var response = ToUserResponse(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var page = await users.ListAsync(effectiveLimit, effectiveOffset, cancellationToken);
        var items = page.Items.Select(ToUserResponse).ToList();
        return Ok(new PagedResponse<UserResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "user_not_found",
                detail: $"No user with id '{id}'."
            );

        return Ok(ToUserResponse(user));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        var newStatus = Enum.Parse<UserStatus>(request.Status, true);
        if (currentUserId == id && newStatus == UserStatus.Suspended)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "cannot_suspend_self",
                detail: "An admin cannot suspend their own account."
            );

        var updated = await users.UpdateStatusAsync(
                          id,
                          newStatus,
                          currentUserId,
                          string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                          cancellationToken
                      );
        if (updated is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "user_not_found",
                detail: $"No user with id '{id}'."
            );

        return Ok(ToUserResponse(updated));
    }

    [HttpGet("{id:guid}/status-history")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(PagedResponse<UserStatusHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatusHistory(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "user_not_found",
                detail: $"No user with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await userStatusHistory.ListForUserAsync(id, effectiveLimit, effectiveOffset, cancellationToken);

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var changerId in page.Items.Select(h => h.ChangedBy).Distinct())
        {
            var changer = await users.FindByIdAsync(changerId, cancellationToken);
            if (changer is not null) emailByUserId[changerId] = changer.Email;
        }

        var items = page.Items
                        .Select(h => new UserStatusHistoryResponse(
                                    h.Id,
                                    h.PreviousStatus.ToString().ToLowerInvariant(),
                                    h.NewStatus.ToString().ToLowerInvariant(),
                                    h.ChangedBy,
                                    emailByUserId.GetValueOrDefault(h.ChangedBy),
                                    h.ChangedAt,
                                    h.Reason)
                        )
                        .ToList();

        return Ok(new PagedResponse<UserStatusHistoryResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null) return Unauthorized();

        return Ok(ToUserResponse(user));
    }

    [HttpGet("/me/licences")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResponse<LicenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMyLicences(
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        LicenceStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LicenceStatus>(status, true, out var s))
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "invalid_status",
                    detail: "status must be one of: active, suspended, revoked."
                );
            parsedStatus = s;
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var owner = await users.FindByIdAsync(userId, cancellationToken);
        if (owner is null) return Unauthorized();
        var page = await licences.ListForOwnerAsync(userId, parsedStatus, effectiveLimit, effectiveOffset, cancellationToken);

        var slugByProductId = new Dictionary<Guid, string>();
        foreach (var pid in page.Items.Select(l => l.ProductId).Distinct())
        {
            var product = await products.FindByIdAsync(pid, cancellationToken);
            if (product is not null) slugByProductId[pid] = product.Slug;
        }

        var items = page.Items
                        .Select(licence => LicencesController.ToLicenceResponse(
                                    licence,
                                    slugByProductId.GetValueOrDefault(licence.ProductId, string.Empty),
                                    owner.Email)
                        )
                        .ToList();

        return Ok(new PagedResponse<LicenceResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/me/licences/{id:guid}/verification-attempts")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResponse<VerificationAttemptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLicenceVerificationAttempts(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null || licence.UserId != userId)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await verificationAttempts.ListForLicenceAsync(
                       id,
                       VerificationAttemptOutcomeFilter.ApprovedOnly,
                       effectiveLimit,
                       effectiveOffset,
                       cancellationToken
                   );

        var items = page.Items.Select(LicencesController.ToVerificationAttemptResponse).ToList();
        return Ok(new PagedResponse<VerificationAttemptResponse>(items, page.Total, effectiveLimit, effectiveOffset));
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
}
