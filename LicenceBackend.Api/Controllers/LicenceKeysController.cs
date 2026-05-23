using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("licences")]
[Authorize]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
public sealed class LicenceKeysController(
    ILicenceRepository licences,
    ILicenceKeyRepository licenceKeys,
    ILicenceCheckoutRepository checkouts,
    ILicenceMemberRepository licenceMembers,
    ILicenceKeyGenerator keyGenerator,
    ILicenceKeyHasher keyHasher,
    IAuditEventRepository auditEvents,
    IUserRepository users,
    TimeProvider time
) : ControllerBase
{
    public const int MaxActiveKeysPerLicence = 5;

    [HttpGet("{id:guid}/keys")]
    [ProducesResponseType(typeof(LicenceKeysResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid id,
        [FromQuery] bool includeRevoked,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var (_, _, error) = await AuthoriseAccessAsync(id, userId, requireMutator: false, cancellationToken);
        if (error is not null) return error;

        var keys = await licenceKeys.ListForLicenceAsync(id, includeRevoked, cancellationToken);
        var activeCount = keys.Count(k => k.IsActive);
        var mapped = keys.Select(ToResponse).ToList();
        return Ok(new LicenceKeysResponse(activeCount, MaxActiveKeysPerLicence, mapped));
    }

    [HttpPost("{id:guid}/keys")]
    [ProducesResponseType(typeof(LicenceKeyMintedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Mint(
        Guid id,
        [FromBody] MintLicenceKeyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var (licence, caller, error) = await AuthoriseAccessAsync(id, userId, requireMutator: true, cancellationToken);
        if (error is not null) return error;

        if (licence!.Status != LicenceStatus.Active)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.InvalidStatus,
                detail: $"Cannot mint a key for a {licence.Status.ToString().ToLowerInvariant()} licence.");

        var rawKey = keyGenerator.Generate();
        var peppered = keyHasher.HashWithActive(rawKey);
        var prefix = ComputeKeyPrefix(rawKey);
        var label = NormaliseOptional(request.Label);
        var reason = NormaliseOptional(request.Reason);

        var outcome = await licenceKeys.MintAsync(id, peppered, prefix, label, userId, MaxActiveKeysPerLicence, cancellationToken);
        switch (outcome)
        {
            case MintKeyOutcome.LicenceNotFound:
                return LicenceNotFound(id);
            case MintKeyOutcome.CapExceeded cap:
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: ProblemTitles.LicenceKeyCapExceeded,
                    detail: $"Licence already has {cap.ActiveCount} active keys (cap {cap.Cap}). Revoke one before minting another.");
            case MintKeyOutcome.Minted minted:
            {
                var actorType = caller!.Role == UserRole.Admin ? AuditActorTypes.Admin : AuditActorTypes.User;
                var payload = new LicenceKeyMintedPayload(
                    minted.Key.Id,
                    Convert.ToBase64String(minted.Key.KeyHmac),
                    minted.Key.KeyHmacPepperVersion,
                    minted.Key.KeyPrefix,
                    minted.Key.Label);
                var auditEvent = AuditEvent.Create(
                    AuditEventTypes.LicenceKeyMinted,
                    AuditSubjectTypes.Licence,
                    id,
                    actorType,
                    userId,
                    reason,
                    payload,
                    time.GetUtcNow());
                await auditEvents.RecordAsync(auditEvent, cancellationToken);
                var response = new LicenceKeyMintedResponse(ToResponse(minted.Key), rawKey);
                return CreatedAtAction(nameof(List), new { id }, response);
            }
            default:
                throw new InvalidOperationException($"Unexpected mint outcome '{outcome.GetType().Name}'.");
        }
    }

    [HttpDelete("{id:guid}/keys/{keyId:guid}")]
    [ProducesResponseType(typeof(LicenceKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid id,
        Guid keyId,
        [FromBody] RevokeLicenceKeyRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var (_, caller, error) = await AuthoriseAccessAsync(id, userId, requireMutator: true, cancellationToken);
        if (error is not null) return error;

        var existing = await licenceKeys.FindByIdAsync(keyId, cancellationToken);
        if (existing is null || existing.LicenceId != id) return KeyNotFound(keyId);

        var reason = NormaliseOptional(request?.Reason);
        var outcome = await licenceKeys.RevokeAsync(keyId, userId, reason, cancellationToken);

        switch (outcome)
        {
            case RevokeKeyOutcome.NotFound:
                return KeyNotFound(keyId);
            case RevokeKeyOutcome.AlreadyRevoked already:
                return Ok(ToResponse(already.Key));
            case RevokeKeyOutcome.Revoked revoked:
            {
                var cascaded = await checkouts.ForceRevokeByLicenceKeyAsync(keyId, userId, reason, cancellationToken);
                var actorType = caller!.Role == UserRole.Admin ? AuditActorTypes.Admin : AuditActorTypes.User;
                var payload = new LicenceKeyRevokedPayload(
                    revoked.Key.Id,
                    Convert.ToBase64String(revoked.Key.KeyHmac),
                    revoked.Key.KeyPrefix,
                    revoked.Key.Label,
                    cascaded);
                var auditEvent = AuditEvent.Create(
                    AuditEventTypes.LicenceKeyRevoked,
                    AuditSubjectTypes.Licence,
                    id,
                    actorType,
                    userId,
                    reason,
                    payload,
                    time.GetUtcNow());
                await auditEvents.RecordAsync(auditEvent, cancellationToken);
                return Ok(ToResponse(revoked.Key));
            }
            default:
                throw new InvalidOperationException($"Unexpected revoke outcome '{outcome.GetType().Name}'.");
        }
    }

    [HttpPatch("{id:guid}/keys/{keyId:guid}")]
    [ProducesResponseType(typeof(LicenceKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLabel(
        Guid id,
        Guid keyId,
        [FromBody] UpdateLicenceKeyLabelRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var (_, caller, error) = await AuthoriseAccessAsync(id, userId, requireMutator: true, cancellationToken);
        if (error is not null) return error;

        var existing = await licenceKeys.FindByIdAsync(keyId, cancellationToken);
        if (existing is null || existing.LicenceId != id) return KeyNotFound(keyId);

        var newLabel = NormaliseOptional(request.Label);
        var updated = await licenceKeys.UpdateLabelAsync(keyId, newLabel, cancellationToken);
        if (updated is null) return KeyNotFound(keyId);

        var actorType = caller!.Role == UserRole.Admin ? AuditActorTypes.Admin : AuditActorTypes.User;
        var payload = new LicenceKeyLabelChangedPayload(keyId, existing.Label, updated.Label);
        var auditEvent = AuditEvent.Create(
            AuditEventTypes.LicenceKeyLabelChanged,
            AuditSubjectTypes.Licence,
            id,
            actorType,
            userId,
            NormaliseOptional(request.Reason),
            payload,
            time.GetUtcNow());
        await auditEvents.RecordAsync(auditEvent, cancellationToken);

        return Ok(ToResponse(updated));
    }

    private async Task<(Licence? Licence, User? Caller, IActionResult? Error)> AuthoriseAccessAsync(
        Guid licenceId,
        Guid currentUserId,
        bool requireMutator,
        CancellationToken cancellationToken)
    {
        var licence = await licences.FindByIdAsync(licenceId, cancellationToken);
        if (licence is null) return (null, null, LicenceNotFound(licenceId));

        var caller = await users.FindByIdAsync(currentUserId, cancellationToken);
        if (caller is null) return (null, null, LicenceNotFound(licenceId));

        var isAdmin = caller.Role == UserRole.Admin;
        var isOwner = licence.UserId == currentUserId;
        if (isAdmin || isOwner) return (licence, caller, null);

        var isMember = await licenceMembers.IsMemberAsync(licenceId, currentUserId, cancellationToken);
        if (!isMember) return (null, null, LicenceNotFound(licenceId));

        if (requireMutator) return (licence, caller, Forbid());
        return (licence, caller, null);
    }

    private static LicenceKeyResponse ToResponse(LicenceKey key) => new(
        key.Id,
        key.LicenceId,
        key.KeyPrefix,
        key.Label,
        key.CreatedByUserId,
        key.CreatedAt,
        key.LastSeenAt,
        key.RevokedAt,
        key.RevokedByUserId,
        key.RevokeReason);

    private static string ComputeKeyPrefix(string rawKey)
        => rawKey.Length > 12 ? $"{rawKey[..12]}..." : $"{rawKey}...";

    private static string? NormaliseOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }

    private IActionResult LicenceNotFound(Guid id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: ProblemTitles.LicenceNotFound,
            detail: $"No licence with id '{id}'.");

    private IActionResult KeyNotFound(Guid keyId) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: ProblemTitles.LicenceKeyNotFound,
            detail: $"No licence key with id '{keyId}'.");
}
