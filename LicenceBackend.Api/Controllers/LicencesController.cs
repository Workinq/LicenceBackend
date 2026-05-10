using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("licences")]
[Authorize(Roles = "admin")]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class LicencesController(
    ILicenceRepository licences,
    ILicenceStatusHistoryRepository licenceStatusHistory,
    ILicenceBindingHistoryRepository bindingHistory,
    ILicenceVerificationAttemptRepository verificationAttempts,
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyGenerator keyGenerator,
    ILicenceKeyHasher keyHasher,
    TimeProvider time
) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [HttpPost]
    [ProducesResponseType(typeof(LicenceCreatedResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLicenceRequest request,
        CancellationToken cancellationToken
    )
    {
        var hasUserId = request.UserId is not null;
        var hasEmail = !string.IsNullOrWhiteSpace(request.Email);
        switch (hasUserId)
        {
            case false when !hasEmail:
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "missing_owner",
                    detail: "Provide either 'userId' or 'email' to identify the licence owner."
                );
            case true when hasEmail:
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "ambiguous_owner",
                    detail: "Provide exactly one of 'userId' or 'email', not both."
                );
        }

        var owner = hasUserId
                        ? await users.FindByIdAsync(request.UserId!.Value, cancellationToken)
                        : await users.FindByEmailAsync(request.Email!, cancellationToken);
        if (owner is null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "owner_not_found",
                detail: hasUserId
                            ? $"No user with id '{request.UserId}'."
                            : $"No user with email '{request.Email}'."
            );

        var product = await products.FindByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "product_not_found",
                detail: $"No product with id '{request.ProductId}'."
            );

        if (request.ExpiresAt is { } expiresAt && expiresAt <= time.GetUtcNow())
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_expires_at",
                detail: "expiresAt must be in the future."
            );

        var rawKey = keyGenerator.Generate();
        var pepperedHmac = keyHasher.HashWithActive(rawKey);
        var now = time.GetUtcNow();
        var licence = new Licence(
            Guid.NewGuid(),
            product.Id,
            owner.Id,
            pepperedHmac.Hmac,
            pepperedHmac.PepperVersion,
            LicenceStatus.Active,
            request.ExpiresAt,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            null,
            null,
            null,
            now,
            now
        );

        await licences.CreateAsync(licence, cancellationToken);

        var response = new LicenceCreatedResponse(
            licence.Id,
            product.Id,
            product.Slug,
            owner.Id,
            owner.Email,
            licence.Status.ToString().ToLowerInvariant(),
            licence.ExpiresAt,
            licence.Notes,
            false,
            null,
            licence.CreatedAt,
            rawKey
        );

        return CreatedAtAction(nameof(GetById), new { id = licence.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<LicenceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? userId,
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
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
        var page = await licences.ListAsync(productId, userId, parsedStatus, effectiveLimit, effectiveOffset, cancellationToken);

        var slugByProductId = new Dictionary<Guid, string>();
        foreach (var pid in page.Items.Select(l => l.ProductId).Distinct())
        {
            var product = await products.FindByIdAsync(pid, cancellationToken);
            if (product is not null) slugByProductId[pid] = product.Slug;
        }

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var uid in page.Items.Select(l => l.UserId).Distinct())
        {
            var user = await users.FindByIdAsync(uid, cancellationToken);
            if (user is not null) emailByUserId[uid] = user.Email;
        }

        var items = page.Items
                        .Select(licence => ToLicenceResponse(
                                    licence,
                                    slugByProductId.GetValueOrDefault(licence.ProductId, string.Empty),
                                    emailByUserId.GetValueOrDefault(licence.UserId, string.Empty))
                        )
                        .ToList();

        return Ok(new PagedResponse<LicenceResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LicenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);

        return Ok(ToLicenceResponse(licence, product?.Slug ?? string.Empty, owner?.Email ?? string.Empty));
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(LicenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateLicenceStatusRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        var newStatus = Enum.Parse<LicenceStatus>(request.Status, true);
        var updated = await licences.UpdateStatusAsync(
                          id,
                          newStatus,
                          currentUserId,
                          string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                          cancellationToken
                      );

        if (updated is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        var product = await products.FindByIdAsync(updated.ProductId, cancellationToken);
        var owner = await users.FindByIdAsync(updated.UserId, cancellationToken);

        return Ok(ToLicenceResponse(updated, product?.Slug ?? string.Empty, owner?.Email ?? string.Empty));
    }

    [HttpGet("{id:guid}/status-history")]
    [ProducesResponseType(typeof(PagedResponse<LicenceStatusHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatusHistory(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await licenceStatusHistory.ListForLicenceAsync(id, effectiveLimit, effectiveOffset, cancellationToken);

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var changerId in page.Items.Select(h => h.ChangedBy).Distinct())
        {
            var changer = await users.FindByIdAsync(changerId, cancellationToken);
            if (changer is not null) emailByUserId[changerId] = changer.Email;
        }

        var items = page.Items
                        .Select(history => new LicenceStatusHistoryResponse(
                                    history.Id,
                                    history.PreviousStatus.ToString().ToLowerInvariant(),
                                    history.NewStatus.ToString().ToLowerInvariant(),
                                    history.ChangedBy,
                                    emailByUserId.GetValueOrDefault(history.ChangedBy),
                                    history.ChangedAt,
                                    history.Reason)
                        )
                        .ToList();

        return Ok(new PagedResponse<LicenceStatusHistoryResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpPut("{id:guid}/hwid")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHwid(
        Guid id,
        [FromBody] UpdateLicenceHwidRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        if (!string.IsNullOrEmpty(request.Hwid))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_hwid",
                detail: "Only clearing is supported: send { \"hwid\": null }. HWIDs are pinned via first-use verify."
            );

        var cleared = await licences.ClearHwidAsync(
                          id,
                          currentUserId,
                          string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                          cancellationToken
                      );

        if (cleared is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        return NoContent();
    }

    [HttpPut("{id:guid}/ip-allowlist")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIpAllowlist(
        Guid id,
        [FromBody] UpdateLicenceIpAllowlistRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Unauthorized();

        IReadOnlyList<string>? cidrs = null;
        if (request.Cidrs is not null)
        {
            if (request.Cidrs.Count == 0)
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "invalid_ip_allowlist",
                    detail: "cidrs must be null (to unrestrict) or a non-empty list."
                );

            var normalised = new List<string>(request.Cidrs.Count);
            foreach (var raw in request.Cidrs)
            {
                if (string.IsNullOrWhiteSpace(raw) || !IPNetwork.TryParse(raw, out _))
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "invalid_ip_allowlist",
                        detail: $"'{raw}' is not a valid CIDR."
                    );
                normalised.Add(raw.Trim());
            }

            cidrs = normalised;
        }

        var updated = await licences.UpdateIpAllowlistAsync(
                          id,
                          cidrs,
                          currentUserId,
                          string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                          cancellationToken
                      );

        if (updated is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        return NoContent();
    }

    [HttpGet("{id:guid}/binding-history")]
    [ProducesResponseType(typeof(PagedResponse<BindingHistoryEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBindingHistory(
        Guid id,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await bindingHistory.ListForLicenceAsync(id, effectiveLimit, effectiveOffset, cancellationToken);

        var items = page.Items.Select(ToBindingHistoryResponse).ToList();
        return Ok(new PagedResponse<BindingHistoryEntryResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("{id:guid}/verification-attempts")]
    [ProducesResponseType(typeof(PagedResponse<VerificationAttemptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVerificationAttempts(
        Guid id,
        [FromQuery] string? outcome,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        var licence = await licences.FindByIdAsync(id, cancellationToken);
        if (licence is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "licence_not_found",
                detail: $"No licence with id '{id}'."
            );

        if (!TryParseOutcomeFilter(outcome, out var filter))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_outcome",
                detail: "outcome must be 'approved' or 'denied'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await verificationAttempts.ListForLicenceAsync(id, filter, effectiveLimit, effectiveOffset, cancellationToken);

        var items = page.Items.Select(ToVerificationAttemptResponse).ToList();
        return Ok(new PagedResponse<VerificationAttemptResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }

    internal static bool TryParseOutcomeFilter(string? outcome, out VerificationAttemptOutcomeFilter filter)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            filter = VerificationAttemptOutcomeFilter.All;
            return true;
        }

        switch (outcome.ToLowerInvariant())
        {
            case "approved":
                filter = VerificationAttemptOutcomeFilter.ApprovedOnly;
                return true;
            case "denied":
                filter = VerificationAttemptOutcomeFilter.DeniedOnly;
                return true;
            default:
                filter = VerificationAttemptOutcomeFilter.All;
                return false;
        }
    }

    internal static LicenceResponse ToLicenceResponse(Licence licence, string productSlug, string userEmail)
    {
        return new LicenceResponse(
            licence.Id,
            licence.ProductId,
            productSlug,
            licence.UserId,
            userEmail,
            licence.Status.ToString().ToLowerInvariant(),
            licence.ExpiresAt,
            licence.Notes,
            licence.HwidHmac is not null,
            licence.IpAllowlist,
            licence.CreatedAt
        );
    }

    internal static BindingHistoryEntryResponse ToBindingHistoryResponse(LicenceBindingHistoryEntry entry)
    {
        return new BindingHistoryEntryResponse(
            entry.Id,
            BindingTypeToString(entry.BindingType),
            ParseJsonElement(entry.PreviousValueJson),
            ParseJsonElement(entry.NewValueJson),
            ChangeSourceToString(entry.ChangeSource),
            entry.ChangedByUserId,
            entry.ChangedAt,
            entry.Reason
        );
    }

    internal static VerificationAttemptResponse ToVerificationAttemptResponse(LicenceVerificationAttempt attempt)
    {
        return new VerificationAttemptResponse(
            attempt.Id,
            attempt.LicenceId,
            attempt.ProductIdRequested,
            attempt.HwidHmac is null ? null : Convert.ToBase64String(attempt.HwidHmac),
            attempt.SourceIp,
            LicenceVerificationAttemptRepository.OutcomeToString(attempt.Outcome),
            LicenceVerificationAttemptRepository.DenialReasonToString(attempt.DenialReason),
            attempt.AttemptedAt
        );
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string BindingTypeToString(LicenceBindingType type)
    {
        return type switch
        {
            LicenceBindingType.Hwid => "hwid",
            LicenceBindingType.IpAllowlist => "ip_allowlist",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static string ChangeSourceToString(BindingChangeSource source)
    {
        return source switch
        {
            BindingChangeSource.Admin => "admin",
            BindingChangeSource.FirstUse => "first_use",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
    }
}
