using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
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
public sealed class LicenceSeatsController(
    ILicenceRepository licences,
    ILicenceMemberRepository licenceMembers,
    ILicenceCheckoutRepository checkouts,
    IUserRepository users
) : ControllerBase
{
    private const int DefaultHistoryLimit = 20;
    private const int MaxHistoryLimit = 100;

    [HttpGet("{id:guid}/seats")]
    [ProducesResponseType(typeof(LicenceSeatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSeats(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null) return LicenceNotFound(id);

        var caller = await users.FindByIdAsync(userId, cancellationToken);
        if (caller is null) return LicenceNotFound(id);

        var isAdmin = caller.Role == UserRole.Admin;
        var isOwner = licence.UserId == userId;
        var isMember = !isOwner && !isAdmin && await licenceMembers.IsMemberAsync(id, userId, cancellationToken);
        if (!isAdmin && !isOwner && !isMember) return LicenceNotFound(id);

        var effectiveLimit = Math.Clamp(limit ?? DefaultHistoryLimit, 1, MaxHistoryLimit);
        var effectiveOffset = Math.Max(0, offset ?? 0);

        var live = await checkouts.ListLiveForLicenceAsync(id, cancellationToken);
        var history = await checkouts.ListHistoryForLicenceAsync(id, effectiveLimit, effectiveOffset, cancellationToken);

        var liveResponses = live.Select(MapLive).ToList();
        var historyResponses = history.Items.Select(MapHistory).ToList();

        return Ok(new LicenceSeatsResponse(
            licence.MaxSeats,
            liveResponses,
            new PagedResponse<LicenceSeatHistoryEntryResponse>(historyResponses, history.Total, effectiveLimit, effectiveOffset)));
    }

    [HttpDelete("{id:guid}/seats/{seatId:guid}")]
    public async Task<IActionResult> ForceRevoke(Guid id, Guid seatId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null) return LicenceNotFound(id);

        var caller = await users.FindByIdAsync(userId, cancellationToken);
        if (caller is null) return LicenceNotFound(id);

        var isAdmin = caller.Role == UserRole.Admin;
        var isOwner = licence.UserId == userId;
        if (!isAdmin && !isOwner)
        {
            var isMember = await licenceMembers.IsMemberAsync(id, userId, cancellationToken);
            return isMember ? Forbid() : LicenceNotFound(id);
        }

        var reason = isAdmin ? LicenceCheckoutCloseReason.AdminRevoked : LicenceCheckoutCloseReason.OwnerRevoked;
        var revoked = await checkouts.ForceRevokeAsync(seatId, reason, userId, actorReason: null, cancellationToken);
        if (!revoked) return SeatNotFound(seatId);

        return NoContent();
    }

    private IActionResult SeatNotFound(Guid seatId) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: ProblemTitles.SeatNotFound,
            detail: $"No live seat with id '{seatId}'.");

    private static LicenceSeatResponse MapLive(LicenceCheckout c) => new(
        c.Id,
        InstanceHashPrefix(c.InstanceIdHash),
        c.MemberUserId,
        c.HwidHmac is null ? null : Convert.ToBase64String(c.HwidHmac),
        c.SourceIp,
        c.IssuedAt,
        c.LastHeartbeatAt,
        c.ExpiresAt);

    private static LicenceSeatHistoryEntryResponse MapHistory(LicenceCheckoutHistoryEntry h) => new(
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
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }

    private IActionResult LicenceNotFound(Guid id) =>
        Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: ProblemTitles.LicenceNotFound,
            detail: $"No licence with id '{id}'.");
}
