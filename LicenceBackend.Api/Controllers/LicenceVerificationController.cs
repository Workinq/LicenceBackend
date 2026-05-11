using System.Net;
using System.Net.Sockets;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
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
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyHasher hasher,
    IHwidHasher hwidHasher,
    ILicenceVerificationSigner signer,
    LicenceVerifySigningKeySet signingKeySet,
    ILicenceVerificationAttemptRepository attempts,
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
        if (string.IsNullOrWhiteSpace(request.LicenceKey)
            || request.ProductId is not { } productId
            || string.IsNullOrWhiteSpace(request.ClientNonce)
            || request.ClientNonce.Length < MinClientNonceLength
            || request.ClientNonce.Length > MaxClientNonceLength
        )
        {
            return InvalidLicence();
        }

        var rateLimitDecision = await verifyRateLimiter.TryAcquireAsync(request.LicenceKey, cancellationToken);
        if (!rateLimitDecision.Acquired) return RateLimitRejection.AsResult(HttpContext, rateLimitDecision.RetryAfter);

        IReadOnlyList<byte[]> keyHmacCandidates;
        try
        {
            keyHmacCandidates = hasher.HashAllVersions(request.LicenceKey);
        }
        catch (ArgumentException)
        {
            return InvalidLicence();
        }

        var licence = await licences.FindByKeyHmacAsync(keyHmacCandidates, cancellationToken);
        if (licence is null) return InvalidLicence();

        var remote = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        var remoteIpText = remote.ToString();
        var now = time.GetUtcNow();
        var (denialReason, presentedHwidHmac, pendingFirstPin) = await DetermineOutcomeAsync(licence, productId, remote, request.Hwid, now, cancellationToken);

        Product? product = null;
        User? owner = null;
        if (denialReason is null)
        {
            product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
            owner = await users.FindByIdAsync(licence.UserId, cancellationToken);
            if (product is null || owner is null)
            {
                denialReason = VerificationDenialReason.LicenceNotUsable;
                pendingFirstPin = null;
            }
        }

        if (denialReason is null && licence.IsIpAutoBindArmed && !remote.Equals(IPAddress.None))
        {
            var bindResult = await licences.BindFirstUseIpAsync(licence.Id, HostRouteFor(remote), cancellationToken);
            switch (bindResult)
            {
                case IpBindResult.Bound:
                    break;
                case IpBindResult.AlreadyBound:
                    var refreshed = await licences.FindByIdAsync(licence.Id, cancellationToken);
                    if (refreshed is null || !refreshed.IsIpAllowed(remote))
                    {
                        denialReason = VerificationDenialReason.IpNotAllowlisted;
                        pendingFirstPin = null;
                    }
                    else
                    {
                        licence = refreshed;
                    }
                    break;
                default:
                    denialReason = VerificationDenialReason.LicenceNotUsable;
                    pendingFirstPin = null;
                    break;
            }
        }

        var attemptId = Guid.NewGuid();
        var auditRecorded = false;
        if (pendingFirstPin is not null && denialReason is null)
        {
            var approvedAttempt = new LicenceVerificationAttempt(
                attemptId,
                licence.Id,
                productId,
                pendingFirstPin.Value.Hmac,
                remoteIpText,
                VerificationOutcome.Approved,
                null,
                now
            );

            var pinResult = await licences.PinHwidAndRecordAttemptAsync(licence.Id, pendingFirstPin.Value.Hmac, pendingFirstPin.Value.PepperVersion, remoteIpText, approvedAttempt, cancellationToken);
            switch (pinResult)
            {
                case PinHwidResult.Pinned:
                    presentedHwidHmac = pendingFirstPin.Value.Hmac;
                    auditRecorded = true;
                    break;
                case PinHwidResult.AlreadyBound:
                    var (raceReason, raceHmac) = await ResolveRaceLoserAsync(licence.Id, request.Hwid!, cancellationToken);
                    denialReason = raceReason;
                    presentedHwidHmac = raceHmac;
                    break;
                case PinHwidResult.NotFound:
                    break;
                default:
                    denialReason = VerificationDenialReason.LicenceNotUsable;
                    presentedHwidHmac = pendingFirstPin.Value.Hmac;
                    break;
            }
        }

        // Re-read licence in case a successful first-pin mutated it
        if (denialReason is null && licence.HwidHmac is null && presentedHwidHmac is not null)
        {
            var refreshed = await licences.FindByIdAsync(licence.Id, cancellationToken);
            if (refreshed is not null) licence = refreshed;
        }

        if (!auditRecorded)
        {
            var outcome = denialReason is null ? VerificationOutcome.Approved : VerificationOutcome.Denied;
            await attempts.RecordAsync(
                new LicenceVerificationAttempt(
                    attemptId,
                    licence.Id,
                    productId,
                    presentedHwidHmac,
                    remoteIpText,
                    outcome,
                    denialReason,
                    now
                ),
                cancellationToken
            );
        }

        if (denialReason is not null)
        {
            logger.LogDebug(
                "Verify denied for licence {LicenceId}: {DenialReason} from {SourceIp}",
                licence.Id,
                denialReason,
                remoteIpText
            );
            return InvalidLicence();
        }

        logger.LogInformation(
            "Verified licence {LicenceId} product {ProductSlug} owner {UserId} from {SourceIp}",
            licence.Id,
            product!.Slug,
            owner!.Id,
            remoteIpText
        );

        var claims = new SignedLicenceVerificationClaims(
            licence.Id,
            licence.ProductId,
            product.Slug,
            licence.Status.ToString().ToLowerInvariant(),
            licence.ExpiresAt,
            licence.Notes,
            request.ClientNonce!
        );

        return Ok(new SignedLicenceVerificationResponse(signer.Sign(claims)));
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

    private IActionResult InvalidLicence()
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: ProblemTitles.InvalidLicence,
            detail: "The licence key is not valid for this request."
        );
    }
}
