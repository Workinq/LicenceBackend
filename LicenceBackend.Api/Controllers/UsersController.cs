using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Orders;
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
    IAuditEventRepository auditEvents,
    ILicenceRepository licences,
    ILicenceKeyRepository licenceKeys,
    ILicenceMemberRepository licenceMembers,
    ILicenceCheckoutRepository checkouts,
    IProductRepository products,
    IProductFileRepository productFiles,
    IProductFileStorage productFileStorage,
    IOrderItemRepository orderItems,
    IPasswordHasher passwordHasher,
    ILicenceKeyGenerator keyGenerator,
    ILicenceKeyHasher keyHasher,
    TimeProvider time
) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;
    private const int LicenceLabelMaxLength = 10;

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
                title: ProblemTitles.EmailAlreadyExists,
                detail: $"A user with email '{request.Email}' already exists."
            );

        var now = time.GetUtcNow();
        var user = new User(
            Guid.NewGuid(),
            request.Email.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            passwordHasher.Hash(request.Password),
            string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            UserRole.User,
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] string? q,
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken cancellationToken
    )
    {
        UserRole? parsedRole = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role, true, out var r))
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.InvalidRole,
                    detail: "role must be one of: user, admin."
                );
            parsedRole = r;
        }

        UserStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<UserStatus>(status, true, out var s))
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.InvalidStatus,
                    detail: "status must be one of: active, suspended."
                );
            parsedStatus = s;
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var page = await users.ListAsync(effectiveLimit, effectiveOffset, q, parsedRole, parsedStatus, cancellationToken);
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
                title: ProblemTitles.UserNotFound,
                detail: $"No user with id '{id}'."
            );

        return Ok(ToUserResponse(user));
    }

    [HttpGet("{id:guid}/licences")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(PagedResponse<LicenceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserLicences(
        Guid id,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var user = await users.FindByIdAsync(id, cancellationToken);
        if (user is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.UserNotFound,
                detail: $"No user with id '{id}'."
            );

        LicenceStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<LicenceStatus>(status, true, out var s))
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.InvalidStatus,
                    detail: "status must be one of: active, suspended, revoked."
                );
            parsedStatus = s;
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await licences.ListForUserAsync(id, parsedStatus, effectiveLimit, effectiveOffset, cancellationToken);

        var slugByProductId = new Dictionary<Guid, string>();
        foreach (var pid in page.Items.Select(l => l.Licence.ProductId).Distinct())
        {
            var product = await products.FindByIdAsync(pid, cancellationToken);
            if (product is not null) slugByProductId[pid] = product.Slug;
        }

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var ownerId in page.Items.Select(l => l.Licence.UserId).Distinct())
        {
            var owner = await users.FindByIdAsync(ownerId, cancellationToken);
            if (owner is not null) emailByUserId[ownerId] = owner.Email;
        }

        var items = page.Items
                        .Select(entry => LicencesController.ToLicenceResponse(
                                    entry.Licence,
                                    slugByProductId.GetValueOrDefault(entry.Licence.ProductId, string.Empty),
                                    emailByUserId.GetValueOrDefault(entry.Licence.UserId, string.Empty),
                                    entry.Relationship)
                        )
                        .ToList();

        return Ok(new PagedResponse<LicenceResponse>(items, page.Total, effectiveLimit, effectiveOffset));
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
                title: ProblemTitles.CannotSuspendSelf,
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
                title: ProblemTitles.UserNotFound,
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
                title: ProblemTitles.UserNotFound,
                detail: $"No user with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await auditEvents.QueryAsync(
                       AuditSubjectTypes.User,
                       id,
                       new[] { AuditEventTypes.UserStatusChanged },
                       effectiveLimit,
                       effectiveOffset,
                       cancellationToken);

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var changerId in page.Items.Select(e => e.ActorUserId).OfType<Guid>().Distinct())
        {
            var changer = await users.FindByIdAsync(changerId, cancellationToken);
            if (changer is not null) emailByUserId[changerId] = changer.Email;
        }

        var items = page.Items
                        .Select(evt =>
                            {
                                var payload = evt.DeserializePayload<UserStatusChangedPayload>();
                                return new UserStatusHistoryResponse(
                                    evt.Id,
                                    payload.PreviousStatus,
                                    payload.NewStatus,
                                    evt.ActorUserId ?? Guid.Empty,
                                    evt.ActorUserId is { } changerId ? emailByUserId.GetValueOrDefault(changerId) : null,
                                    evt.OccurredAt,
                                    evt.Reason);
                            })
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

    [HttpPatch("/me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var updated = await users.UpdateDisplayNameAsync(userId, request.DisplayName, cancellationToken);
        if (updated is null) return Unauthorized();

        return Ok(ToUserResponse(updated));
    }

    [HttpPatch("/me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null) return Unauthorized();

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.InvalidCredentials,
                detail: "Current password is incorrect."
            );

        var newHash = passwordHasher.Hash(request.NewPassword);
        var currentSid = TryGetCurrentSessionId(out var sid) ? sid : (Guid?)null;
        var updated = await users.UpdatePasswordAsync(userId, newHash, currentSid, cancellationToken);
        if (updated is null) return Unauthorized();

        return NoContent();
    }

    private bool TryGetCurrentSessionId(out Guid sessionId)
    {
        var sidClaim = User.FindFirst("sid")?.Value;
        return Guid.TryParse(sidClaim, out sessionId);
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
                    title: ProblemTitles.InvalidStatus,
                    detail: "status must be one of: active, suspended, revoked."
                );
            parsedStatus = s;
        }

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var caller = await users.FindByIdAsync(userId, cancellationToken);
        if (caller is null) return Unauthorized();
        var page = await licences.ListForUserAsync(userId, parsedStatus, effectiveLimit, effectiveOffset, cancellationToken);

        var slugByProductId = new Dictionary<Guid, string>();
        foreach (var pid in page.Items.Select(l => l.Licence.ProductId).Distinct())
        {
            var product = await products.FindByIdAsync(pid, cancellationToken);
            if (product is not null) slugByProductId[pid] = product.Slug;
        }

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var ownerId in page.Items.Select(l => l.Licence.UserId).Distinct())
        {
            var owner = await users.FindByIdAsync(ownerId, cancellationToken);
            if (owner is not null) emailByUserId[ownerId] = owner.Email;
        }

        var items = page.Items
                        .Select(entry => LicencesController.ToLicenceResponse(
                                    entry.Licence,
                                    slugByProductId.GetValueOrDefault(entry.Licence.ProductId, string.Empty),
                                    emailByUserId.GetValueOrDefault(entry.Licence.UserId, string.Empty),
                                    entry.Relationship)
                        )
                        .ToList();

        return Ok(new PagedResponse<LicenceResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/me/licences/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(LicenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLicence(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null) return LicenceNotFound(id);

        var isOwner = licence.UserId == userId;
        var isMember = !isOwner && await licenceMembers.IsMemberAsync(id, userId, cancellationToken);
        if (!isOwner && !isMember) return LicenceNotFound(id);

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);
        var relationship = isOwner ? UserLicenceRelationships.Owner : UserLicenceRelationships.Member;
        var orderId = await orderItems.FindOrderIdByLicenceIdAsync(licence.Id, cancellationToken);
        return Ok(LicencesController.ToLicenceResponse(licence, product?.Slug ?? string.Empty, owner?.Email ?? string.Empty, relationship, orderId));
    }

    [HttpGet("/me/licences/{id:guid}/download")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMyLicenceFile(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null) return LicenceNotFound(id);

        var isOwner = licence.UserId == userId;
        var isMember = !isOwner && await licenceMembers.IsMemberAsync(id, userId, cancellationToken);
        if (!isOwner && !isMember) return LicenceNotFound(id);

        var now = time.GetUtcNow();
        if (!licence.IsUsableAt(now)) return LicenceNotFound(id);

        var file = await productFiles.GetLatestForProductAsync(licence.ProductId, cancellationToken);
        if (file is null) return LicenceNotFound(id);

        var stream = await productFileStorage.OpenReadAsync(file.StoragePath, cancellationToken);
        if (stream is null) return LicenceNotFound(id);

        var evt = AuditEvent.Create(
            AuditEventTypes.ProductFileDownloaded,
            AuditSubjectTypes.Product,
            licence.ProductId,
            AuditActorTypes.User,
            userId,
            reason: null,
            new ProductFileDownloadedPayload(file.Id, file.VersionNumber, licence.Id),
            now);
        await auditEvents.RecordAsync(evt, cancellationToken);

        return File(stream, file.ContentType, file.FileName);
    }

    [HttpPatch("/me/licences/{id:guid}/label")]
    [Authorize]
    [ProducesResponseType(typeof(LicenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateMyLicenceLabel(
        Guid id,
        [FromBody] UpdateLicenceLabelRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var trimmed = string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim();
        if (trimmed is not null && trimmed.Length > LicenceLabelMaxLength)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.LabelTooLong,
                detail: $"Label must be {LicenceLabelMaxLength} characters or fewer."
            );

        var updated = await licences.UpdateLabelAsync(id, userId, trimmed, cancellationToken);
        if (updated is null)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: ProblemTitles.LicenceNotOwned,
                detail: "You do not own this licence."
            );

        var product = await products.FindByIdAsync(updated.ProductId, cancellationToken);
        var owner = await users.FindByIdAsync(updated.UserId, cancellationToken);
        return Ok(LicencesController.ToLicenceResponse(updated, product?.Slug ?? string.Empty, owner?.Email ?? string.Empty, UserLicenceRelationships.Owner));
    }

    [HttpGet("/me/licences/{id:guid}/members")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<LicenceMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMyLicenceMembers(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null || licence.UserId != userId) return LicenceNotFound(id);

        var responses = await BuildMemberResponsesAsync(id, cancellationToken);
        return Ok(responses);
    }

    [HttpPost("/me/licences/{id:guid}/members")]
    [Authorize]
    [ProducesResponseType(typeof(LicenceMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMyLicenceMember(
        Guid id,
        [FromBody] AddLicenceMemberRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null || licence.UserId != userId) return LicenceNotFound(id);

        var memberUser = await users.FindByEmailAsync(request.Email.Trim(), cancellationToken);
        if (memberUser is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.UserNotFound,
                detail: $"No user with email '{request.Email}'."
            );

        if (memberUser.Id == licence.UserId)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: ProblemTitles.MemberIsOwner,
                detail: "The owner of a licence cannot also be a member."
            );

        if (await licenceMembers.IsMemberAsync(id, memberUser.Id, cancellationToken))
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: ProblemTitles.MemberAlreadyExists,
                detail: $"User '{memberUser.Email}' is already a member of this licence."
            );

        var now = time.GetUtcNow();
        await licenceMembers.AddAsync(new LicenceMember(id, memberUser.Id, userId, now), cancellationToken);

        var auditEvent = AuditEvent.Create(
            AuditEventTypes.LicenceMemberAdded,
            AuditSubjectTypes.Licence,
            id,
            AuditActorTypes.Admin,
            userId,
            null,
            new LicenceMemberChangedPayload(memberUser.Id, memberUser.Email),
            now
        );
        await auditEvents.RecordAsync(auditEvent, cancellationToken);

        var actor = await users.FindByIdAsync(userId, cancellationToken);
        var response = new LicenceMemberResponse(
            memberUser.Id,
            memberUser.Email,
            memberUser.DisplayName,
            userId,
            actor?.Email,
            now
        );
        return CreatedAtAction(nameof(ListMyLicenceMembers), new { id }, response);
    }

    [HttpDelete("/me/licences/{id:guid}/members/{memberId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMyLicenceMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null || licence.UserId != userId) return LicenceNotFound(id);

        var memberUser = await users.FindByIdAsync(memberId, cancellationToken);
        var removed = await licenceMembers.RemoveAsync(id, memberId, cancellationToken);
        if (!removed)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.UserNotFound,
                detail: $"User '{memberId}' is not a member of this licence."
            );

        var auditEvent = AuditEvent.Create(
            AuditEventTypes.LicenceMemberRemoved,
            AuditSubjectTypes.Licence,
            id,
            AuditActorTypes.Admin,
            userId,
            null,
            new LicenceMemberChangedPayload(memberId, memberUser?.Email ?? string.Empty),
            time.GetUtcNow()
        );
        await auditEvents.RecordAsync(auditEvent, cancellationToken);

        return NoContent();
    }

    [HttpPost("/me/licences/{id:guid}/regenerate-key")]
    [Authorize]
    [ProducesResponseType(typeof(LicenceKeyRegeneratedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateMyLicenceKey(
        Guid id,
        [FromBody] RegenerateLicenceKeyRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null || licence.UserId != userId) return LicenceNotFound(id);

        if (licence.Status != LicenceStatus.Active)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.InvalidStatus,
                detail: $"Cannot regenerate the key for a {licence.Status.ToString().ToLowerInvariant()} licence."
            );

        var rawKey = keyGenerator.Generate();
        var pepperedHmac = keyHasher.HashWithActive(rawKey);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        var existingActiveKeys = await licenceKeys.ListForLicenceAsync(id, includeRevoked: false, cancellationToken);
        var previousKey = existingActiveKeys.OrderByDescending(k => k.CreatedAt).FirstOrDefault();
        foreach (var existingKey in existingActiveKeys)
        {
            await licenceKeys.RevokeAsync(existingKey.Id, userId, "regenerate", cancellationToken);
        }
        var keyPrefix = rawKey.Length > 12 ? $"{rawKey[..12]}..." : $"{rawKey}...";
        var mint = await licenceKeys.MintAsync(
            id,
            pepperedHmac,
            keyPrefix,
            label: null,
            createdByUserId: userId,
            activeCap: int.MaxValue,
            cancellationToken);
        if (mint is not MintKeyOutcome.Minted)
            throw new InvalidOperationException("Failed to mint regenerated licence key");

        var regenEvent = AuditEvent.Create(
            AuditEventTypes.LicenceKeyRegenerated,
            AuditSubjectTypes.Licence,
            id,
            AuditActorTypes.User,
            userId,
            reason,
            new LicenceKeyRegeneratedPayload(
                previousKey is null ? null : Convert.ToBase64String(previousKey.KeyHmac),
                previousKey?.KeyHmacPepperVersion,
                Convert.ToBase64String(pepperedHmac.Hmac),
                pepperedHmac.PepperVersion
            ),
            time.GetUtcNow()
        );
        await auditEvents.RecordAsync(regenEvent, cancellationToken);

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);

        return Ok(new LicenceKeyRegeneratedResponse(
            licence.Id,
            licence.ProductId,
            product?.Slug ?? string.Empty,
            licence.UserId,
            owner?.Email ?? string.Empty,
            licence.Status.ToString().ToLowerInvariant(),
            licence.ExpiresAt,
            licence.Notes,
            licence.HwidHmac is not null,
            licence.IpAllowlist,
            licence.Label,
            licence.CreatedAt,
            rawKey
        ));
    }

    private async Task<IReadOnlyList<LicenceMemberResponse>> BuildMemberResponsesAsync(Guid licenceId, CancellationToken cancellationToken)
    {
        var rows = await licenceMembers.ListByLicenceAsync(licenceId, cancellationToken);
        if (rows.Count == 0) return Array.Empty<LicenceMemberResponse>();

        var distinctUserIds = rows.Select(r => r.UserId).Concat(rows.Select(r => r.AddedBy)).Distinct();
        var userById = new Dictionary<Guid, User>();
        foreach (var uid in distinctUserIds)
        {
            var u = await users.FindByIdAsync(uid, cancellationToken);
            if (u is not null) userById[uid] = u;
        }

        return rows.Select(r =>
        {
            var memberUser = userById.GetValueOrDefault(r.UserId);
            var addedByUser = userById.GetValueOrDefault(r.AddedBy);
            return new LicenceMemberResponse(
                r.UserId,
                memberUser?.Email ?? string.Empty,
                memberUser?.DisplayName,
                r.AddedBy,
                addedByUser?.Email,
                r.AddedAt
            );
        }).ToList();
    }

    private ObjectResult LicenceNotFound(Guid id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: ProblemTitles.LicenceNotFound,
        detail: $"No licence with id '{id}'."
    );

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
                title: ProblemTitles.LicenceNotFound,
                detail: $"No licence with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await auditEvents.QueryVerifiesAsync(
                       id,
                       VerificationOutcomeNames.Approved,
                       effectiveLimit,
                       effectiveOffset,
                       cancellationToken
                   );

        var items = page.Items.Select(LicencesController.ToVerificationAttemptResponse).ToList();
        return Ok(new PagedResponse<VerificationAttemptResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/me/licences/{id:guid}/seats")]
    [Authorize]
    [ProducesResponseType(typeof(LicenceSeatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyLicenceSeats(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null) return LicenceNotFound(id);

        var isOwner = licence.UserId == userId;
        var isMember = !isOwner && await licenceMembers.IsMemberAsync(id, userId, cancellationToken);
        if (!isOwner && !isMember) return LicenceNotFound(id);

        var effectiveLimit = Math.Clamp(limit ?? 20, 1, 100);
        var effectiveOffset = Math.Max(0, offset ?? 0);

        var live = await checkouts.ListLiveForLicenceAsync(id, cancellationToken);
        var history = await checkouts.ListHistoryForLicenceAsync(id, effectiveLimit, effectiveOffset, cancellationToken);

        var liveResponses = live.Select(MapLiveSeat).ToList();
        var historyResponses = history.Items.Select(MapHistorySeat).ToList();

        return Ok(new LicenceSeatsResponse(
            licence.MaxSeats,
            liveResponses,
            new PagedResponse<LicenceSeatHistoryEntryResponse>(historyResponses, history.Total, effectiveLimit, effectiveOffset)));
    }

    private static LicenceSeatResponse MapLiveSeat(LicenceCheckout c) => new(
        c.Id,
        InstanceHashPrefix(c.InstanceIdHash),
        c.MemberUserId,
        c.HwidHmac is null ? null : Convert.ToBase64String(c.HwidHmac),
        c.SourceIp,
        c.IssuedAt,
        c.LastHeartbeatAt,
        c.ExpiresAt);

    private static LicenceSeatHistoryEntryResponse MapHistorySeat(LicenceCheckoutHistoryEntry h) => new(
        h.Id,
        h.CheckoutId,
        InstanceHashPrefix(h.InstanceIdHash),
        h.MemberUserId,
        h.HwidHmac is null ? null : Convert.ToBase64String(h.HwidHmac),
        h.SourceIp,
        h.IssuedAt,
        h.ClosedAt,
        LicenceCheckoutCloseReasonNames.ToString(h.CloseReason));

    private static string InstanceHashPrefix(byte[] hash) =>
        Convert.ToHexString(hash.AsSpan(0, Math.Min(8, hash.Length))).ToLowerInvariant();

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
