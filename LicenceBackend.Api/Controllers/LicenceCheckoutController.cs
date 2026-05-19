using System.Net;
using System.Security.Cryptography;
using System.Text;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("licences")]
[AllowAnonymous]
public sealed class LicenceCheckoutController(
    ILicenceRepository licences,
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyHasher keyHasher,
    IHwidHasher hwidHasher,
    ILicenceCheckoutRepository checkouts,
    ILicenceVerificationSigner signer,
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
        if (string.IsNullOrWhiteSpace(request.LicenceKey)
            || request.ProductId is not { } productId
            || string.IsNullOrWhiteSpace(request.ClientNonce)
            || request.ClientNonce.Length < MinClientNonceLength
            || request.ClientNonce.Length > MaxClientNonceLength
            || string.IsNullOrWhiteSpace(request.InstanceId)
            || request.InstanceId.Length < _options.MinInstanceIdLength
            || request.InstanceId.Length > _options.MaxInstanceIdLength)
        {
            return InvalidLicence();
        }

        IReadOnlyList<byte[]> keyHmacCandidates;
        try
        {
            keyHmacCandidates = keyHasher.HashAllVersions(request.LicenceKey);
        }
        catch (ArgumentException)
        {
            return InvalidLicence();
        }

        var licence = await licences.FindByKeyHmacAsync(keyHmacCandidates, cancellationToken);
        if (licence is null) return InvalidLicence();
        if (licence.ProductId != productId) return InvalidLicence();

        var now = time.GetUtcNow();
        if (!licence.IsUsableAt(now)) return InvalidLicence();

        var owner = await users.FindByIdAsync(licence.UserId, cancellationToken);
        if (owner is null || owner.Status != UserStatus.Active) return InvalidLicence();

        var remote = HttpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        if (!licence.IsIpAllowed(remote)) return InvalidLicence();

        var product = await products.FindByIdAsync(licence.ProductId, cancellationToken);
        if (product is null) return InvalidLicence();

        PepperedHmac? hwidPepperedHmac = null;
        if (!string.IsNullOrWhiteSpace(request.Hwid))
        {
            try
            {
                hwidPepperedHmac = hwidHasher.HashWithActive(request.Hwid);
            }
            catch (ArgumentException)
            {
                return InvalidLicence();
            }
        }

        if (licence.HwidHmac is not null)
        {
            if (hwidPepperedHmac is null) return InvalidLicence();
            if (!licence.IsHwidAllowed(hwidPepperedHmac.Value.Hmac)) return InvalidLicence();
        }

        var instanceIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.InstanceId));

        var outcome = await checkouts.OpenAsync(
            licence.Id,
            instanceIdHash,
            memberUserId: null,
            hwidHmac: hwidPepperedHmac?.Hmac,
            hwidHmacPepperVersion: hwidPepperedHmac?.PepperVersion,
            sourceIp: remote.ToString(),
            leaseDuration: TimeSpan.FromSeconds(_options.LeaseSeconds),
            cancellationToken);

        return outcome switch
        {
            OpenCheckoutOutcome.LicenceNotFound => InvalidLicence(),
            OpenCheckoutOutcome.DeniedNoSeats denied => NoSeatsAvailable(denied.Detail),
            OpenCheckoutOutcome.Opened opened => Ok(BuildSignedResponse(licence, product.Slug, opened.Result.Checkout, request.ClientNonce!)),
            _ => InvalidLicence()
        };
    }

    [HttpDelete("checkouts/{seatId:guid}")]
    public async Task<IActionResult> Checkin(Guid seatId, CancellationToken cancellationToken)
    {
        await checkouts.CloseAsync(seatId, cancellationToken);
        return NoContent();
    }

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

    private IActionResult InvalidLicence() =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: ProblemTitles.InvalidLicence,
            detail: "The licence key is not valid for this request.");

    private IActionResult SeatGone() =>
        Problem(
            statusCode: StatusCodes.Status410Gone,
            title: ProblemTitles.SeatGone,
            detail: "Seat does not exist or has expired. Re-checkout to obtain a new seat.");

    private IActionResult NoSeatsAvailable(DeniedNoSeatsResult detail) =>
        StatusCode(StatusCodes.Status409Conflict, new NoSeatsAvailableResponse(
            ProblemTitles.NoSeatsAvailable,
            detail.MaxSeats,
            detail.ActiveSeats,
            detail.OldestExpiresAt));
}
