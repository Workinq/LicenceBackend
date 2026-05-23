using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Invoices;
using LicenceBackend.Core.Licences;
using LicenceBackend.Core.Orders;
using LicenceBackend.Core.Payments;
using LicenceBackend.Core.Products;
using Npgsql;

namespace LicenceBackend.Infrastructure.Payments;

public sealed class OrderFulfillmentService(
    NpgsqlDataSource dataSource,
    ICheckoutAttemptRepository checkoutAttempts,
    IOrderRepository orders,
    IOrderItemRepository orderItems,
    IInvoiceRepository invoices,
    ILicenceRepository licences,
    ILicenceKeyRepository licenceKeys,
    ILicenceKeyGenerator keyGenerator,
    ILicenceKeyHasher keyHasher,
    IAuditEventRepository auditEvents,
    IProductRepository products,
    TimeProvider time) : IOrderFulfillmentService
{
    public async Task<Guid> FulfillAsync(Guid checkoutAttemptId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var locked = await checkoutAttempts.LockByIdInTxAsync(connection, transaction, checkoutAttemptId, cancellationToken);
            if (locked is null)
                throw new InvalidOperationException($"Checkout attempt '{checkoutAttemptId}' was not found.");

            var (attempt, items) = locked.Value;

            if (TryHandleAlreadyTerminal(attempt, checkoutAttemptId, out var earlyOrderId))
            {
                await transaction.CommitAsync(cancellationToken);
                return earlyOrderId;
            }

            var now = time.GetUtcNow();
            var orderId = Guid.NewGuid();

            var productById = await LoadProductsAsync(items, cancellationToken);
            var (orderItemEntities, createdLicences) = await CreateLicencesAndOrderItemsAsync(
                connection, transaction, attempt, items, productById, orderId, now, cancellationToken);

            await PersistOrderAsync(connection, transaction, attempt, orderItemEntities, orderId, now, cancellationToken);
            await PersistInvoiceAsync(connection, transaction, attempt, orderItemEntities, createdLicences, orderId, now, cancellationToken);
            await RecordAuditEventsAsync(connection, transaction, attempt, orderItemEntities, createdLicences, orderId, now, cancellationToken);

            await checkoutAttempts.MarkFulfilledInTxAsync(connection, transaction, attempt.Id, orderId, now, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return orderId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static bool TryHandleAlreadyTerminal(CheckoutAttempt attempt, Guid checkoutAttemptId, out Guid orderId)
    {
        if (attempt.Status == CheckoutAttemptStatus.Fulfilled)
        {
            orderId = attempt.OrderId
                      ?? throw new InvalidOperationException($"Fulfilled attempt '{checkoutAttemptId}' has no order id.");
            return true;
        }

        if (attempt.Status == CheckoutAttemptStatus.Failed)
            throw new InvalidOperationException($"Checkout attempt '{checkoutAttemptId}' is marked failed.");

        orderId = Guid.Empty;
        return false;
    }

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(
        IReadOnlyList<CheckoutAttemptItem> items,
        CancellationToken cancellationToken)
    {
        var productById = new Dictionary<Guid, Product>();
        foreach (var productId in items.Select(i => i.ProductId).Distinct())
        {
            var product = await products.FindByIdAsync(productId, cancellationToken)
                          ?? throw new InvalidOperationException($"Product '{productId}' for the attempt no longer exists.");
            productById[productId] = product;
        }
        return productById;
    }

    private async Task<(List<OrderItem> OrderItems, List<(Licence Licence, Product Product)> CreatedLicences)> CreateLicencesAndOrderItemsAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CheckoutAttempt attempt,
        IReadOnlyList<CheckoutAttemptItem> items,
        Dictionary<Guid, Product> productById,
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var orderItemEntities = new List<OrderItem>();
        var createdLicences = new List<(Licence Licence, Product Product)>();

        foreach (var item in items)
        {
            var product = productById[item.ProductId];
            for (var unit = 0; unit < item.Quantity; unit++)
            {
                var label = ResolveLabel(item.Labels, unit);
                var licence = await CreateLicenceAsync(connection, transaction, attempt.UserId, item.ProductId, label, now, cancellationToken);

                orderItemEntities.Add(new OrderItem(
                    Guid.NewGuid(),
                    orderId,
                    item.ProductId,
                    licence.Id,
                    item.UnitPrice,
                    item.Currency,
                    now));
                createdLicences.Add((licence, product));
            }
        }

        return (orderItemEntities, createdLicences);
    }

    private static string? ResolveLabel(IReadOnlyList<string?> labels, int unit)
    {
        var labelInput = unit < labels.Count ? labels[unit] : null;
        return string.IsNullOrWhiteSpace(labelInput) ? null : labelInput.Trim();
    }

    private async Task<Licence> CreateLicenceAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        Guid userId,
        Guid productId,
        string? label,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var licence = new Licence(
            Guid.NewGuid(),
            productId,
            userId,
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

        var rawKey = keyGenerator.Generate();
        var pepperedHmac = keyHasher.HashWithActive(rawKey);
        var keyPrefix = BuildKeyPrefix(rawKey);
        var mintParameters = new MintLicenceKeyParameters(
            LicenceId: licence.Id,
            PepperedHmac: pepperedHmac,
            KeyPrefix: keyPrefix,
            Label: null,
            CreatedByUserId: null,
            ActiveCap: 5);
        var mintOutcome = await licenceKeys.MintInTxAsync(connection, transaction, mintParameters, cancellationToken);
        if (mintOutcome is not MintKeyOutcome.Minted)
            throw new InvalidOperationException($"Failed to mint initial licence key for licence '{licence.Id}'.");

        return licence;
    }

    private async Task PersistOrderAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CheckoutAttempt attempt,
        List<OrderItem> orderItemEntities,
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var order = new Order(orderId, attempt.UserId, attempt.ContactEmail, OrderStatus.Completed, now);
        await orders.CreateInTxAsync(connection, transaction, order, cancellationToken);
        await orderItems.BulkCreateInTxAsync(connection, transaction, orderItemEntities, cancellationToken);
    }

    private async Task PersistInvoiceAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CheckoutAttempt attempt,
        List<OrderItem> orderItemEntities,
        List<(Licence Licence, Product Product)> createdLicences,
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invoiceId = Guid.NewGuid();
        var invoiceLineItems = createdLicences.Select((entry, index) =>
        {
            var orderItem = orderItemEntities[index];
            return new InvoiceLineItem(
                Guid.NewGuid(),
                invoiceId,
                entry.Product.Id,
                entry.Licence.Id,
                entry.Product.DisplayName,
                entry.Product.Slug,
                entry.Licence.Label,
                orderItem.UnitPrice,
                orderItem.Currency);
        }).ToList();

        var invoice = new Invoice(
            invoiceId,
            orderId,
            InvoiceNumber: 0,
            IssuedAt: now,
            ContactEmail: attempt.ContactEmail,
            BuyerName: null,
            BuyerAddressLine1: null,
            BuyerAddressLine2: null,
            BuyerCity: null,
            BuyerRegion: null,
            BuyerPostalCode: null,
            BuyerCountry: null);
        await invoices.CreateInTxAsync(connection, transaction, invoice, invoiceLineItems, cancellationToken);
    }

    private async Task RecordAuditEventsAsync(
        Npgsql.NpgsqlConnection connection,
        Npgsql.NpgsqlTransaction transaction,
        CheckoutAttempt attempt,
        List<OrderItem> orderItemEntities,
        List<(Licence Licence, Product Product)> createdLicences,
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var totals = orderItemEntities
            .GroupBy(i => i.Currency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyTotal(g.Key, g.Sum(i => i.UnitPrice ?? 0m)))
            .ToList();

        var orderPlacedEvt = AuditEvent.Create(new AuditEventDraft(
            AuditEventTypes.OrderPlaced,
            AuditSubjectTypes.Order,
            orderId,
            AuditActorTypes.User,
            attempt.UserId,
            Reason: null,
            new OrderPlacedPayload(orderItemEntities.Count, totals, attempt.ContactEmail),
            now));
        await auditEvents.RecordInTxAsync(connection, transaction, orderPlacedEvt, cancellationToken);

        foreach (var entry in createdLicences)
        {
            var licenceCreatedEvt = AuditEvent.Create(new AuditEventDraft(
                AuditEventTypes.LicenceCreated,
                AuditSubjectTypes.Licence,
                entry.Licence.Id,
                AuditActorTypes.User,
                attempt.UserId,
                Reason: null,
                new LicenceCreatedPayload(orderId, entry.Product.Id, entry.Product.Price, entry.Product.Currency, entry.Licence.Label),
                now));
            await auditEvents.RecordInTxAsync(connection, transaction, licenceCreatedEvt, cancellationToken);
        }
    }

    private static string BuildKeyPrefix(string rawKey)
        => rawKey.Length > 12 ? $"{rawKey[..12]}..." : $"{rawKey}...";
}
