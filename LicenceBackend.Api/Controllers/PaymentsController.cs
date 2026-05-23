using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Core.Payments;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("payments")]
[Authorize]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class PaymentsController(
    ICheckoutAttemptRepository checkoutAttempts,
    IProductRepository products,
    IUserRepository users,
    IPaymentGateway paymentGateway,
    IOrderFulfillmentService fulfillment,
    TimeProvider time,
    IOptions<StripeOptions> stripeOptions) : ControllerBase
{
    private const int MaxLabelLength = 10;

    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Checkout([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var buyerId)) return Unauthorized();
        var buyer = await users.FindByIdAsync(buyerId, cancellationToken);
        if (buyer is null) return Unauthorized();

        if (ValidateItems(request.Items) is { } itemsError) return itemsError;

        if (!TryResolveContactEmail(request.ContactEmail, buyer.Email, out var contactEmail, out var emailError))
            return emailError!;

        var resolvedResult = await ResolveProductsAsync(request.Items, cancellationToken);
        if (resolvedResult.Error is not null) return resolvedResult.Error;
        var resolved = resolvedResult.Resolved!;

        var currencies = resolved.Select(r => r.Product.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (currencies.Count > 1)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.MixedCurrencyCart,
                detail: "All items in a checkout must share one currency.");
        var currency = currencies[0];

        var amountTotal = resolved.Sum(r => (r.Product.Price ?? 0m) * r.Quantity);

        var now = time.GetUtcNow();
        var attemptId = Guid.NewGuid();
        var items = resolved.Select(r => new CheckoutAttemptItem(
            Guid.NewGuid(), attemptId, r.Product.Id, r.Quantity, r.Labels, r.Product.Price, r.Product.Currency)).ToList();

        return amountTotal <= 0m
            ? await FulfillFreeOrderAsync(attemptId, buyerId, contactEmail, currency, items, now, cancellationToken)
            : await CreatePaidCheckoutAsync(attemptId, buyerId, contactEmail, currency, amountTotal, items, now, cancellationToken);
    }

    private IActionResult? ValidateItems(IReadOnlyList<CreateOrderItemRequest> requestItems)
    {
        if (requestItems.Count == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.EmptyOrder,
                detail: "At least one item is required.");

        foreach (var item in requestItems)
        {
            if (item.Quantity < 1)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidQuantity,
                    detail: "Quantity must be at least 1.");

            var labels = item.Labels ?? Array.Empty<string?>();
            if (labels.Count != item.Quantity)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.LabelCountMismatch,
                    detail: $"Item for product '{item.ProductId}' has quantity {item.Quantity} but {labels.Count} label entries; they must match.");

            if (FindTooLongLabel(labels) is not null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.LabelTooLong,
                    detail: $"Labels must be {MaxLabelLength} characters or fewer.");
        }

        return null;
    }

    private static string? FindTooLongLabel(IReadOnlyList<string?> labels)
    {
        foreach (var label in labels)
        {
            if (label is not null && label.Trim().Length > MaxLabelLength) return label;
        }
        return null;
    }

    private bool TryResolveContactEmail(string? requested, string fallback, out string contactEmail, out IActionResult? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(requested))
        {
            contactEmail = fallback;
            return true;
        }

        var candidate = requested.Trim();
        if (!MailAddress.TryCreate(candidate, out _))
        {
            contactEmail = string.Empty;
            error = Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemTitles.InvalidContactEmail,
                detail: "contactEmail is not a valid email address.");
            return false;
        }

        contactEmail = candidate;
        return true;
    }

    private async Task<(List<(Product Product, int Quantity, IReadOnlyList<string?> Labels)>? Resolved, IActionResult? Error)> ResolveProductsAsync(
        IReadOnlyList<CreateOrderItemRequest> requestItems,
        CancellationToken cancellationToken)
    {
        var resolved = new List<(Product Product, int Quantity, IReadOnlyList<string?> Labels)>();
        foreach (var item in requestItems)
        {
            var product = await products.FindByIdAsync(item.ProductId, cancellationToken);
            if (product is null)
                return (null, Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.ProductNotFound,
                    detail: $"No product with id '{item.ProductId}'."));
            if (!product.IsPublic)
                return (null, Problem(statusCode: StatusCodes.Status403Forbidden, title: ProblemTitles.ProductNotPurchasable,
                    detail: $"Product '{product.Slug}' is not available for purchase."));
            resolved.Add((product, item.Quantity, item.Labels ?? Array.Empty<string?>()));
        }
        return (resolved, null);
    }

    private async Task<IActionResult> FulfillFreeOrderAsync(
        Guid attemptId,
        Guid buyerId,
        string contactEmail,
        string currency,
        List<CheckoutAttemptItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var freeAttempt = new CheckoutAttempt(
            attemptId, buyerId, contactEmail, currency, 0m,
            $"free_{attemptId:N}", CheckoutAttemptStatus.Pending, null, now, null);
        await checkoutAttempts.CreateAsync(freeAttempt, items, cancellationToken);
        var orderId = await fulfillment.FulfillAsync(attemptId, cancellationToken);
        return Ok(new CheckoutSessionResponse(null, null, orderId, Free: true));
    }

    private async Task<IActionResult> CreatePaidCheckoutAsync(
        Guid attemptId,
        Guid buyerId,
        string contactEmail,
        string currency,
        decimal amountTotal,
        List<CheckoutAttemptItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var amountMinorUnits = (long)Math.Round(amountTotal * 100m, MidpointRounding.AwayFromZero);
        var intent = await paymentGateway.CreatePaymentIntentAsync(amountMinorUnits, currency, cancellationToken);

        var attempt = new CheckoutAttempt(
            attemptId, buyerId, contactEmail, currency, amountTotal,
            intent.PaymentIntentId, CheckoutAttemptStatus.Pending, null, now, null);
        await checkoutAttempts.CreateAsync(attempt, items, cancellationToken);

        return Ok(new CheckoutSessionResponse(intent.ClientSecret, attemptId, null, Free: false));
    }

    [HttpGet("checkout/{id:guid}")]
    [ProducesResponseType(typeof(CheckoutStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var attempt = await checkoutAttempts.FindByIdAsync(id, cancellationToken);
        if (attempt is null || attempt.UserId != userId)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: ProblemTitles.CheckoutAttemptNotFound,
                detail: $"No checkout attempt with id '{id}'.");
        return Ok(new CheckoutStatusResponse(attempt.Status.ToString().ToLowerInvariant(), attempt.OrderId));
    }

    [HttpGet("config")]
    [ProducesResponseType(typeof(PaymentConfigResponse), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
        => Ok(new PaymentConfigResponse(stripeOptions.Value.PublishableKey));

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }
}
