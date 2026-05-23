using System.Net;
using System.Net.Sockets;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("licences/verify")]
[AllowAnonymous]
public sealed class LicenceVerificationController(
    ILicenceRepository licences,
    ILicenceKeyRepository licenceKeys,
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyHasher hasher,
    IHwidHasher hwidHasher,
    ILicenceVerificationSigner signer,
    LicenceVerifySigningKeySet signingKeySet,
    IAuditEventRepository auditEvents,
    ILicenceVerifyRateLimiter verifyRateLimiter,
    TimeProvider time,
    ILogger<LicenceVerificationController> logger
) : ControllerBase
{
    private const int MinClientNonceLength = 16;
    private const int MaxClientNonceLength = 128;
    private const string SigningAlgorithm = "ES256";

    [HttpPost]
    [ProducesResponseType(typeof(SignedLicenceVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Verify(
        [FromBody] VerifyLicenceRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!IsRequestShapeValid(request, out var productId)) return InvalidLicence();

        var rateLimitDecision = await verifyRateLimiter.TryAcquireAsync(request.LicenceKey!, cancellationToken);
        if (!rateLimitDecision.Acquired) return RateLimitRejection.AsResult(HttpContext, rateLimitDecision.RetryAfter);

        var keyLookup = await TryFindActiveKeyAsync(request.LicenceKey!, cancellationToken);
        if (keyLookup is null) return InvalidLicence();
        var (key, licence) = keyLookup.Value;

        var remote = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        var remoteIpText = remote.ToString();
        var now = time.GetUtcNow();

        var state = await EvaluateVerificationAsync(licence, productId, remote, remoteIpText, request, now, cancellationToken);

        if (!state.AuditRecorded)
            await RecordAuditAsync(state, productId, remoteIpText, now, cancellationToken);

        if (state.DenialReason is not null)
        {
            logger.LogDebug(
                "Verify denied for licence {LicenceId}: {DenialReason} from {SourceIp}",
                state.Licence.Id,
                state.DenialReason,
                remoteIpText
            );
            return InvalidLicence();
        }

        return await BuildSuccessResponseAsync(state, key, request.ClientNonce!, remoteIpText, now, cancellationToken);
    }

    private static bool IsRequestShapeValid(VerifyLicenceRequest request, out Guid productId)
    {
        productId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(request.LicenceKey)) return false;
        if (request.ProductId is not { } pid) return false;
        if (string.IsNullOrWhiteSpace(request.ClientNonce)) return false;
        if (request.ClientNonce.Length < MinClientNonceLength) return false;
        if (request.ClientNonce.Length > MaxClientNonceLength) return false;
        productId = pid;
        return true;
    }

    private async Task<(LicenceKey Key, Licence Licence)?> TryFindActiveKeyAsync(string licenceKey, CancellationToken cancellationToken)
    {
        IReadOnlyList<byte[]> keyHmacCandidates;
        try
        {
            keyHmacCandidates = hasher.HashAllVersions(licenceKey);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var key = await licenceKeys.FindActiveByKeyHmacAsync(keyHmacCandidates, cancellationToken);
        if (key is null) return null;
        var licence = await licences.FindByIdAsync(key.LicenceId, cancellationToken);
        if (licence is null) return null;
        return (key, licence);
    }

    private async Task<VerificationState> EvaluateVerificationAsync(
        Licence licence,
        Guid productId,
        IPAddress remote,
        string remoteIpText,
        VerifyLicenceRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (denialReason, presentedHwidHmac, pendingFirstPin) =
            await DetermineOutcomeAsync(licence, productId, remote, request.Hwid, now, cancellationToken);

        var state = new VerificationState(licence, denialReason, presentedHwidHmac, pendingFirstPin);

        await LoadProductAndOwnerAsync(state, cancellationToken);
        await ApplyIpAutoBindAsync(state, remote, cancellationToken);
        await ApplyHwidFirstPinAsync(state, productId, remoteIpText, request.Hwid, now, cancellationToken);
        await RefreshLicenceAfterPinAsync(state, cancellationToken);

        return state;
    }

    private async Task LoadProductAndOwnerAsync(VerificationState state, CancellationToken cancellationToken)
    {
        if (state.DenialReason is not null) return;

        state.Product = await products.FindByIdAsync(state.Licence.ProductId, cancellationToken);
        state.Owner = await users.FindByIdAsync(state.Licence.UserId, cancellationToken);
        if (state.Product is null || state.Owner is null)
        {
            state.DenialReason = VerificationDenialReason.LicenceNotUsable;
            state.PendingFirstPin = null;
        }
    }

    private async Task ApplyIpAutoBindAsync(VerificationState state, IPAddress remote, CancellationToken cancellationToken)
    {
        if (state.DenialReason is not null) return;
        if (!state.Licence.IsIpAutoBindArmed || remote.Equals(IPAddress.None)) return;

        var bindResult = await licences.BindFirstUseIpAsync(state.Licence.Id, HostRouteFor(remote), cancellationToken);
        switch (bindResult)
        {
            case IpBindResult.Bound:
                return;
            case IpBindResult.AlreadyBound:
                await HandleAlreadyBoundAsync(state, remote, cancellationToken);
                return;
            default:
                state.DenialReason = VerificationDenialReason.LicenceNotUsable;
                state.PendingFirstPin = null;
                return;
        }
    }

    private async Task HandleAlreadyBoundAsync(VerificationState state, IPAddress remote, CancellationToken cancellationToken)
    {
        var refreshed = await licences.FindByIdAsync(state.Licence.Id, cancellationToken);
        if (refreshed is null || !refreshed.IsIpAllowed(remote))
        {
            state.DenialReason = VerificationDenialReason.IpNotAllowlisted;
            state.PendingFirstPin = null;
        }
        else
        {
            state.Licence = refreshed;
        }
    }

    private async Task ApplyHwidFirstPinAsync(
        VerificationState state,
        Guid productId,
        string remoteIpText,
        string? presentedHwid,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (state.PendingFirstPin is null || state.DenialReason is not null) return;

        var pin = state.PendingFirstPin.Value;
        var pinResult = await licences.PinHwidAndRecordAttemptAsync(
            state.Licence.Id, pin.Hmac, pin.PepperVersion, productId, remoteIpText, now, cancellationToken);

        switch (pinResult)
        {
            case PinHwidResult.Pinned:
                state.PresentedHwidHmac = pin.Hmac;
                state.AuditRecorded = true;
                return;
            case PinHwidResult.AlreadyBound:
                var (raceReason, raceHmac) = await ResolveRaceLoserAsync(state.Licence.Id, presentedHwid!, cancellationToken);
                state.DenialReason = raceReason;
                state.PresentedHwidHmac = raceHmac;
                return;
            case PinHwidResult.NotFound:
                return;
            default:
                state.DenialReason = VerificationDenialReason.LicenceNotUsable;
                state.PresentedHwidHmac = pin.Hmac;
                return;
        }
    }

    private async Task RefreshLicenceAfterPinAsync(VerificationState state, CancellationToken cancellationToken)
    {
        if (state.DenialReason is not null) return;
        if (state.Licence.HwidHmac is not null || state.PresentedHwidHmac is null) return;

        var refreshed = await licences.FindByIdAsync(state.Licence.Id, cancellationToken);
        if (refreshed is not null) state.Licence = refreshed;
    }

    private async Task RecordAuditAsync(
        VerificationState state,
        Guid productId,
        string remoteIpText,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = new LicenceVerifiedPayload(
            productId,
            state.PresentedHwidHmac is null ? null : Convert.ToBase64String(state.PresentedHwidHmac),
            remoteIpText,
            state.DenialReason is null ? VerificationOutcomeNames.Approved : VerificationOutcomeNames.Denied,
            VerificationDenialReasonNames.ToString(state.DenialReason)
        );
        var evt = AuditEvent.Create(new AuditEventDraft(
            AuditEventTypes.LicenceVerified,
            AuditSubjectTypes.Licence,
            state.Licence.Id,
            AuditActorTypes.Anonymous,
            ActorUserId: null,
            Reason: null,
            payload,
            now
        ));
        await auditEvents.RecordAsync(evt, cancellationToken);
    }

    private async Task<IActionResult> BuildSuccessResponseAsync(
        VerificationState state,
        LicenceKey key,
        string clientNonce,
        string remoteIpText,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Verified licence {LicenceId} product {ProductSlug} owner {UserId} from {SourceIp}",
            state.Licence.Id,
            state.Product!.Slug,
            state.Owner!.Id,
            remoteIpText
        );

        var claims = new SignedLicenceVerificationClaims(
            state.Licence.Id,
            state.Licence.ProductId,
            state.Product.Slug,
            state.Licence.Status.ToString().ToLowerInvariant(),
            state.Licence.ExpiresAt,
            state.Licence.Notes,
            clientNonce
        );

        try
        {
            await licenceKeys.BumpLastSeenAsync(key.Id, now, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to bump last_seen_at for licence key {KeyId}", key.Id);
        }

        return Ok(new SignedLicenceVerificationResponse(signer.Sign(claims)));
    }

    private sealed class VerificationState(
        Licence licence,
        VerificationDenialReason? denialReason,
        byte[]? presentedHwidHmac,
        PepperedHmac? pendingFirstPin)
    {
        public Licence Licence { get; set; } = licence;
        public VerificationDenialReason? DenialReason { get; set; } = denialReason;
        public byte[]? PresentedHwidHmac { get; set; } = presentedHwidHmac;
        public PepperedHmac? PendingFirstPin { get; set; } = pendingFirstPin;
        public Product? Product { get; set; }
        public User? Owner { get; set; }
        public bool AuditRecorded { get; set; }
    }

    [HttpGet("public-key")]
    [EnableRateLimiting(RateLimiterPolicyNames.VerifyPublicKey)]
    [ProducesResponseType(typeof(JwksResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ObjectResult PublicKey()
    {
        var entries = (
            from securityKey in signingKeySet.AllSecurityKeys
            let parameters = securityKey.ECDsa.ExportParameters(false)
            select new JwkEntry(
                "EC",
                "P-256",
                Base64UrlEncoder.Encode(parameters.Q.X!),
                Base64UrlEncoder.Encode(parameters.Q.Y!),
                securityKey.KeyId,
                SigningAlgorithm,
                "sig"
            )
        ).ToList();
        return Ok(new JwksResponse(entries));
    }

    private async Task<(VerificationDenialReason? Reason, byte[]? PresentedHwidHmac, PepperedHmac? PendingFirstPin)> DetermineOutcomeAsync(
        Licence licence,
        Guid requestedProductId,
        IPAddress remote,
        string? presentedHwid,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (licence.ProductId != requestedProductId) return (VerificationDenialReason.ProductMismatch, null, null);

        if (!licence.IsUsableAt(now)) return (VerificationDenialReason.LicenceNotUsable, null, null);

        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);
        if (owner is null || owner.Status != UserStatus.Active) return (VerificationDenialReason.OwnerSuspended, null, null);

        if (!licence.IsIpAllowed(remote)) return (VerificationDenialReason.IpNotAllowlisted, null, null);

        var hwidProvided = !string.IsNullOrWhiteSpace(presentedHwid);

        if (licence.HwidHmac is null)
        {
            if (!hwidProvided) return (null, null, null);

            PepperedHmac firstPinHash;
            try
            {
                firstPinHash = hwidHasher.HashWithActive(presentedHwid!);
            }
            catch (ArgumentException)
            {
                return (null, null, null);
            }

            return (null, null, firstPinHash);
        }

        if (!hwidProvided) return (VerificationDenialReason.HwidMissing, null, null);

        if (!hwidHasher.TryHashWithVersion(presentedHwid!, licence.HwidHmacPepperVersion!.Value, out var presentedHmac))
            return (VerificationDenialReason.HwidMismatch, null, null);

        return licence.IsHwidAllowed(presentedHmac)
                   ? (null, presentedHmac, null)
                   : (VerificationDenialReason.HwidMismatch, presentedHmac, null);
    }

    private async Task<(VerificationDenialReason? Reason, byte[]? PresentedHwidHmac)> ResolveRaceLoserAsync(
        Guid licenceId,
        string presentedHwid,
        CancellationToken cancellationToken)
    {
        var refreshed = await licences.FindByIdAsync(licenceId, cancellationToken);
        if (refreshed is null || refreshed.HwidHmac is null || refreshed.HwidHmacPepperVersion is null)
            return (VerificationDenialReason.LicenceNotUsable, null);

        if (!hwidHasher.TryHashWithVersion(presentedHwid, refreshed.HwidHmacPepperVersion.Value, out var presentedHmac))
            return (VerificationDenialReason.HwidMismatch, null);

        return refreshed.IsHwidAllowed(presentedHmac)
                   ? (null, presentedHmac)
                   : (VerificationDenialReason.HwidMismatch, presentedHmac);
    }

    private static string HostRouteFor(IPAddress address)
    {
        var prefix = address.AddressFamily == AddressFamily.InterNetworkV6 ? 128 : 32;
        return FormattableString.Invariant($"{address}/{prefix}");
    }

    private ObjectResult InvalidLicence()
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: ProblemTitles.InvalidLicence,
            detail: "The licence key is not valid for this request."
        );
    }
}
