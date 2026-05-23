using System.Net;
using System.Security.Cryptography;
using System.Text;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Options;
using LicenceBackend.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("licences")]
[AllowAnonymous]
public sealed class LicenceCheckoutController(
    ILicenceRepository licences,
    ILicenceKeyRepository licenceKeys,
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyHasher keyHasher,
    IHwidHasher hwidHasher,
    ILicenceCheckoutRepository checkouts,
    ILicenceVerificationSigner signer,
    ILicenceCheckoutRateLimiter checkoutRateLimiter,
    IOptions<LicenceCheckoutOptions> options,
    TimeProvider time
) : ControllerBase
{
    private const int MinClientNonceLength = 16;
    private const int MaxClientNonceLength = 128;
    private readonly LicenceCheckoutOptions _options = options.Value;

    [HttpPost("checkout")]
    [ProducesResponseType(typeof(SignedLicenceCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(NoSeatsAvailableResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutLicenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsCheckoutRequestShapeValid(request, out var productId)) return InvalidLicence();

        var rateLimitDecision = await checkoutRateLimiter.TryAcquireAsync(request.LicenceKey!, request.InstanceId!, cancellationToken);
        if (!rateLimitDecision.Acquired) return RateLimitRejection.AsResult(HttpContext, rateLimitDecision.RetryAfter);

        var resolved = await ResolveLicenceForCheckoutAsync(request, productId, cancellationToken);
        if (resolved is null) return InvalidLicence();
        var (key, licence, product) = resolved.Value;

        var hwidResolution = TryResolveHwid(request.Hwid, licence);
        if (!hwidResolution.IsValid) return InvalidLicence();
        var hwidPepperedHmac = hwidResolution.Hmac;

        var remote = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        var instanceIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.InstanceId!));

        var openParameters = new OpenCheckoutParameters(
            LicenceId: licence.Id,
            InstanceIdHash: instanceIdHash,
            MemberUserId: null,
            HwidHmac: hwidPepperedHmac?.Hmac,
            HwidHmacPepperVersion: hwidPepperedHmac?.PepperVersion,
            SourceIp: remote.ToString(),
            IssuedWithLicenceKeyId: key.Id,
            LeaseDuration: TimeSpan.FromSeconds(_options.LeaseSeconds));
        var outcome = await checkouts.OpenAsync(openParameters, cancellationToken);

        return outcome switch
        {
            OpenCheckoutOutcome.LicenceNotFound => InvalidLicence(),
            OpenCheckoutOutcome.DeniedNoSeats denied => NoSeatsAvailable(denied.Detail),
            OpenCheckoutOutcome.Opened opened => Ok(BuildSignedResponse(licence, product.Slug, opened.Result.Checkout, request.ClientNonce!)),
            _ => InvalidLicence()
        };
    }

    private bool IsCheckoutRequestShapeValid(CheckoutLicenceRequest request, out Guid productId)
    {
        productId = default;
        if (string.IsNullOrWhiteSpace(request.LicenceKey)) return false;
        if (request.ProductId is not { } pid) return false;
        if (string.IsNullOrWhiteSpace(request.ClientNonce)) return false;
        if (request.ClientNonce.Length < MinClientNonceLength) return false;
        if (request.ClientNonce.Length > MaxClientNonceLength) return false;
        if (string.IsNullOrWhiteSpace(request.InstanceId)) return false;
        if (request.InstanceId.Length < _options.MinInstanceIdLength) return false;
        if (request.InstanceId.Length > _options.MaxInstanceIdLength) return false;
        productId = pid;
        return true;
    }

    private async Task<(LicenceKey Key, Licence Licence, Product Product)?> ResolveLicenceForCheckoutAsync(
        CheckoutLicenceRequest request,
        Guid productId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<byte[]> keyHmacCandidates;
        try
        {
            keyHmacCandidates = keyHasher.HashAllVersions(request.LicenceKey!);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var key = await licenceKeys.FindActiveByKeyHmacAsync(keyHmacCandidates, cancellationToken);
        if (key is null) return null;
        var licence = await licences.FindByIdAsync(key.LicenceId, cancellationToken);
        if (licence is null || licence.ProductId != productId) return null;

        var now = time.GetUtcNow();
        if (!licence.IsUsableAt(now)) return null;

        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);
        if (owner is null || owner.Status != UserStatus.Active) return null;

        var remote = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        if (!licence.IsIpAllowed(remote)) return null;

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        if (product is null) return null;

        return (key, licence, product);
    }

    private HwidResolution TryResolveHwid(string? hwid, Licence licence)
    {
        PepperedHmac? hwidPepperedHmac = null;
        if (!string.IsNullOrWhiteSpace(hwid))
        {
            try
            {
                hwidPepperedHmac = hwidHasher.HashWithActive(hwid);
            }
            catch (ArgumentException)
            {
                return HwidResolution.Invalid;
            }
        }

        if (licence.HwidHmac is not null)
        {
            if (hwidPepperedHmac is null) return HwidResolution.Invalid;
            if (!licence.IsHwidAllowed(hwidPepperedHmac.Value.Hmac)) return HwidResolution.Invalid;
        }

        return new HwidResolution(true, hwidPepperedHmac);
    }

    private readonly record struct HwidResolution(bool IsValid, PepperedHmac? Hmac)
    {
        public static HwidResolution Invalid => new(false, null);
    }

    [EnableRateLimiting(RateLimiterPolicyNames.CheckoutCheckin)]
    [HttpDelete("checkouts/{seatId:guid}")]
    public async Task<IActionResult> Checkin(Guid seatId, CancellationToken cancellationToken)
    {
        await checkouts.CloseAsync(seatId, cancellationToken);
        return NoContent();
    }

    [EnableRateLimiting(RateLimiterPolicyNames.CheckoutHeartbeat)]
    [HttpPost("checkouts/{seatId:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(
        Guid seatId,
        [FromBody] CheckoutHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientNonce)
            || request.ClientNonce.Length < MinClientNonceLength
            || request.ClientNonce.Length > MaxClientNonceLength)
        {
            return InvalidLicence();
        }

        var refreshed = await checkouts.HeartbeatAsync(
            seatId,
            TimeSpan.FromSeconds(_options.LeaseSeconds),
            cancellationToken);
        if (refreshed is null) return SeatGone();

        var licence = await licences.FindByIdAsync(refreshed.LicenceId, cancellationToken);
        if (licence is null) return SeatGone();

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        if (product is null) return SeatGone();

        return Ok(BuildSignedResponse(licence, product.Slug, refreshed, request.ClientNonce));
    }

    private SignedLicenceCheckoutResponse BuildSignedResponse(
        Licence licence,
        string productSlug,
        LicenceCheckout checkout,
        string clientNonce)
    {
        var heartbeatHint = checkout.IssuedAt.AddSeconds(_options.HeartbeatHintSeconds);
        var claims = new SignedLicenceVerificationClaims(
            licence.Id,
            licence.ProductId,
            productSlug,
            licence.Status.ToString().ToLowerInvariant(),
            licence.ExpiresAt,
            licence.Notes,
            clientNonce,
            checkout.Id,
            checkout.ExpiresAt,
            heartbeatHint);
        return new SignedLicenceCheckoutResponse(signer.Sign(claims));
    }

    private ObjectResult InvalidLicence() =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: ProblemTitles.InvalidLicence,
            detail: "The licence key is not valid for this request.");

    private ObjectResult SeatGone() =>
        Problem(
            statusCode: StatusCodes.Status410Gone,
            title: ProblemTitles.SeatGone,
            detail: "Seat does not exist or has expired. Re-checkout to obtain a new seat.");

    private ObjectResult NoSeatsAvailable(DeniedNoSeatsResult detail) =>
        StatusCode(StatusCodes.Status409Conflict, new NoSeatsAvailableResponse(
            ProblemTitles.NoSeatsAvailable,
            detail.MaxSeats,
            detail.ActiveSeats,
            detail.OldestExpiresAt));
}
