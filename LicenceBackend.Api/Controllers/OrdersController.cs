using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using LicenceBackend.Api.Models.Request;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Orders;
using LicenceBackend.Core.Products;
using LicenceBackend.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class OrdersController(
    NpgsqlDataSource dataSource,
    IOrderRepository orders,
    IOrderItemRepository orderItems,
    ILicenceRepository licences,
    IAuditEventRepository auditEvents,
    IProductRepository products,
    IUserRepository users,
    ILicenceKeyGenerator keyGenerator,
    ILicenceKeyHasher keyHasher,
    TimeProvider time
) : ControllerBase
{
    private const int MaxLabelLength = 10;

    [HttpPost]
    [ProducesResponseType(typeof(OrderCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var buyerId)) return Unauthorized();
        var buyer = await users.FindByIdAsync(buyerId, cancellationToken);
        if (buyer is null) return Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.EmptyOrder,
                detail: "At least one item is required."
            );

        foreach (var item in request.Items)
        {
            if (item.Quantity < 1)
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.InvalidQuantity,
                    detail: "Quantity must be at least 1."
                );

            var labels = item.Labels ?? Array.Empty<string?>();
            if (labels.Count != item.Quantity)
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.LabelCountMismatch,
                    detail: $"Item for product '{item.ProductId}' has quantity {item.Quantity} but {labels.Count} label entries; they must match."
                );

            foreach (var label in labels)
            {
                if (label is not null && label.Trim().Length > MaxLabelLength)
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: ProblemTitles.LabelTooLong,
                        detail: $"Labels must be {MaxLabelLength} characters or fewer."
                    );
            }
        }

        string contactEmail;
        if (string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            contactEmail = buyer.Email;
        }
        else
        {
            var candidate = request.ContactEmail.Trim();
            if (!IsValidEmail(candidate))
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: ProblemTitles.InvalidContactEmail,
                    detail: "contactEmail is not a valid email address."
                );
            contactEmail = candidate;
        }

        var resolved = new List<ResolvedItem>();
        foreach (var item in request.Items)
        {
            var product = await products.FindByIdAsync(item.ProductId, cancellationToken);
            if (product is null)
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: ProblemTitles.ProductNotFound,
                    detail: $"No product with id '{item.ProductId}'."
                );

            if (!product.IsPublic)
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: ProblemTitles.ProductNotPurchasable,
                    detail: $"Product '{product.Slug}' is not available for purchase."
                );

            resolved.Add(new ResolvedItem(product, item.Quantity, item.Labels ?? Array.Empty<string?>()));
        }

        var now = time.GetUtcNow();
        var orderId = Guid.NewGuid();

        var createdLicences = new List<(Licence Licence, string RawKey, Product Product)>();
        var orderItemEntities = new List<OrderItem>();
        var orderItemResponses = new List<OrderItemCreatedResponse>();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in resolved)
            {
                for (var unit = 0; unit < item.Quantity; unit++)
                {
                    var rawKey = keyGenerator.Generate();
                    var hashed = keyHasher.HashWithActive(rawKey);
                    var labelInput = item.Labels[unit];
                    var label = string.IsNullOrWhiteSpace(labelInput) ? null : labelInput.Trim();

                    var licence = new Licence(
                        Guid.NewGuid(),
                        item.Product.Id,
                        buyerId,
                        hashed.Hmac,
                        hashed.PepperVersion,
                        LicenceStatus.Active,
                        ExpiresAt: null,
                        Notes: null,
                        HwidHmac: null,
                        HwidHmacPepperVersion: null,
                        IpAllowlist: null,
                        Label: label,
                        MaxSeats: 1,
                        CreatedAt: now,
                        UpdatedAt: now);
                    await licences.CreateInTxAsync(connection, transaction, licence, cancellationToken);

                    var orderItem = new OrderItem(
                        Guid.NewGuid(),
                        orderId,
                        item.Product.Id,
                        licence.Id,
                        item.Product.Price,
                        item.Product.Currency,
                        now);
                    orderItemEntities.Add(orderItem);

                    createdLicences.Add((licence, rawKey, item.Product));
                    orderItemResponses.Add(new OrderItemCreatedResponse(
                        orderItem.Id,
                        item.Product.Id,
                        item.Product.Slug,
                        item.Product.DisplayName,
                        licence.Id,
                        label,
                        item.Product.Price,
                        item.Product.Currency,
                        rawKey));
                }
            }

            var order = new Order(orderId, buyerId, contactEmail, OrderStatus.Completed, now);
            await orders.CreateInTxAsync(connection, transaction, order, cancellationToken);
            await orderItems.BulkCreateInTxAsync(connection, transaction, orderItemEntities, cancellationToken);

            var totals = ComputeTotals(orderItemEntities);
            var orderPlacedEvt = AuditEvent.Create(
                AuditEventTypes.OrderPlaced,
                AuditSubjectTypes.Order,
                orderId,
                AuditActorTypes.User,
                buyerId,
                reason: null,
                new OrderPlacedPayload(orderItemEntities.Count, totals, contactEmail),
                now);
            await auditEvents.RecordInTxAsync(connection, transaction, orderPlacedEvt, cancellationToken);

            foreach (var entry in createdLicences)
            {
                var licenceCreatedEvt = AuditEvent.Create(
                    AuditEventTypes.LicenceCreated,
                    AuditSubjectTypes.Licence,
                    entry.Licence.Id,
                    AuditActorTypes.User,
                    buyerId,
                    reason: null,
                    new LicenceCreatedPayload(orderId, entry.Product.Id, entry.Product.Price, entry.Product.Currency, entry.Licence.Label),
                    now);
                await auditEvents.RecordInTxAsync(connection, transaction, licenceCreatedEvt, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            var response = new OrderCreatedResponse(
                orderId,
                buyerId,
                contactEmail,
                OrderStatus.Completed.ToString().ToLowerInvariant(),
                now,
                totals.Select(t => new CurrencyTotalResponse(t.Currency, t.Amount)).ToList(),
                orderItemResponses);

            return CreatedAtAction(nameof(GetMyById), new { id = orderId }, response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

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

        var responses = await BuildOrderResponsesAsync(new[] { order }, cancellationToken);
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

        var responses = await BuildOrderResponsesAsync(new[] { order }, cancellationToken);
        return Ok(responses[0]);
    }

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

    private static IReadOnlyList<CurrencyTotal> ComputeTotals(IReadOnlyList<OrderItem> items)
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

    private static bool IsValidEmail(string candidate)
    {
        return MailAddress.TryCreate(candidate, out _);
    }

    private sealed record ResolvedItem(Product Product, int Quantity, IReadOnlyList<string?> Labels);
}
