using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Core.Invoices;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Orders;
using LicenceBackend.Core.Products;
using LicenceBackend.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class OrdersController(
    IOrderRepository orders,
    IOrderItemRepository orderItems,
    IInvoiceRepository invoices,
    ILicenceRepository licences,
    IProductRepository products,
    IOptions<InvoicingOptions> invoicingOptions
) : ControllerBase
{
    [HttpGet("/me/orders")]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMine([FromQuery] int? limit, [FromQuery] int? offset, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var effectiveLimit = Math.Clamp(limit ?? 50, 1, 200);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var page = await orders.ListByUserAsync(userId, effectiveLimit, effectiveOffset, cancellationToken);
        var responses = await BuildOrderResponsesAsync(page.Items, cancellationToken);
        return Ok(new PagedResponse<OrderResponse>(responses, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/me/orders/{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var order = await orders.FindByIdAsync(id, cancellationToken);
        if (order is null || order.UserId != userId)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.OrderNotFound,
                detail: $"No order with id '{id}'."
            );

        var responses = await BuildOrderResponsesAsync([order], cancellationToken);
        return Ok(responses[0]);
    }

    [HttpGet("/admin/orders")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAdmin(
        [FromQuery] Guid? userId,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(limit ?? 50, 1, 200);
        var effectiveOffset = Math.Max(offset ?? 0, 0);

        var page = await orders.ListAsync(userId, effectiveLimit, effectiveOffset, cancellationToken);
        var responses = await BuildOrderResponsesAsync(page.Items, cancellationToken);
        return Ok(new PagedResponse<OrderResponse>(responses, page.Total, effectiveLimit, effectiveOffset));
    }

    [HttpGet("/admin/orders/{id:guid}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.FindByIdAsync(id, cancellationToken);
        if (order is null)
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: ProblemTitles.OrderNotFound,
                detail: $"No order with id '{id}'."
            );

        var responses = await BuildOrderResponsesAsync([order], cancellationToken);
        return Ok(responses[0]);
    }

    [HttpGet("/me/orders/{id:guid}/invoice")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyInvoice(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var order = await orders.FindByIdAsync(id, cancellationToken);
        if (order is null || order.UserId != userId) return InvoiceNotFound(id);

        return await BuildInvoiceResultAsync(order, cancellationToken);
    }

    [HttpGet("/admin/orders/{id:guid}/invoice")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminInvoice(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.FindByIdAsync(id, cancellationToken);
        if (order is null) return InvoiceNotFound(id);

        return await BuildInvoiceResultAsync(order, cancellationToken);
    }

    private async Task<IActionResult> BuildInvoiceResultAsync(Order order, CancellationToken cancellationToken)
    {
        var found = await invoices.FindByOrderIdAsync(order.Id, cancellationToken);
        if (found is null) return InvoiceNotFound(order.Id);

        var (invoice, lineItems) = found.Value;
        var opts = invoicingOptions.Value;

        var seller = new InvoiceSellerResponse(
            opts.SellerName,
            opts.SellerAddressLine1,
            opts.SellerAddressLine2,
            opts.SellerCity,
            opts.SellerRegion,
            opts.SellerPostalCode,
            opts.SellerCountry);

        var buyer = new InvoiceBuyerResponse(
            invoice.ContactEmail,
            invoice.BuyerName,
            invoice.BuyerAddressLine1,
            invoice.BuyerAddressLine2,
            invoice.BuyerCity,
            invoice.BuyerRegion,
            invoice.BuyerPostalCode,
            invoice.BuyerCountry);

        var lines = lineItems.Select(li => new InvoiceLineItemResponse(
            li.LicenceId,
            li.ProductId,
            li.ProductName,
            li.ProductSlug,
            li.Label,
            li.UnitPrice,
            li.Currency)).ToList();

        var totals = lineItems
            .GroupBy(li => li.Currency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyTotalResponse(g.Key, g.Sum(li => li.UnitPrice ?? 0m)))
            .ToList();

        var response = new InvoiceResponse(
            order.Id,
            opts.FormatNumber(invoice.InvoiceNumber),
            invoice.IssuedAt,
            order.Status.ToString().ToLowerInvariant(),
            seller,
            buyer,
            lines,
            totals);

        return Ok(response);
    }

    private ObjectResult InvoiceNotFound(Guid id) => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: ProblemTitles.InvoiceNotFound,
        detail: $"No invoice for order '{id}'.");

    private async Task<IReadOnlyList<OrderResponse>> BuildOrderResponsesAsync(IReadOnlyList<Order> orderList, CancellationToken cancellationToken)
    {
        if (orderList.Count == 0) return Array.Empty<OrderResponse>();

        var items = await orderItems.ListByOrderIdsAsync(orderList.Select(o => o.Id).ToList(), cancellationToken);
        var itemsByOrder = items.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var productById = new Dictionary<Guid, Product>();
        foreach (var pid in productIds)
        {
            var product = await products.FindByIdAsync(pid, cancellationToken);
            if (product is not null) productById[pid] = product;
        }

        var labelByLicenceId = new Dictionary<Guid, string?>();
        foreach (var licenceId in items.Select(i => i.LicenceId).Distinct())
        {
            var licence = await licences.FindByIdAsync(licenceId, cancellationToken);
            if (licence is not null) labelByLicenceId[licenceId] = licence.Label;
        }

        return orderList.Select(order =>
        {
            var ownItems = itemsByOrder.GetValueOrDefault(order.Id) ?? new List<OrderItem>();
            var totals = ComputeTotals(ownItems);
            var lineItems = ownItems.Select(i =>
            {
                var product = productById.GetValueOrDefault(i.ProductId);
                return new OrderItemResponse(
                    i.Id,
                    i.ProductId,
                    product?.Slug ?? string.Empty,
                    product?.DisplayName ?? string.Empty,
                    i.LicenceId,
                    labelByLicenceId.GetValueOrDefault(i.LicenceId),
                    i.UnitPrice,
                    i.Currency);
            }).ToList();

            return new OrderResponse(
                order.Id,
                order.UserId,
                order.ContactEmail,
                order.Status.ToString().ToLowerInvariant(),
                order.CreatedAt,
                totals.Select(t => new CurrencyTotalResponse(t.Currency, t.Amount)).ToList(),
                lineItems);
        }).ToList();
    }

    private static List<CurrencyTotal> ComputeTotals(IReadOnlyList<OrderItem> items)
    {
        return items
            .GroupBy(i => i.Currency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(i => i.UnitPrice ?? 0m)))
            .ToList();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(subClaim, out userId);
    }
}
