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

            if (attempt.Status == CheckoutAttemptStatus.Fulfilled)
            {
                await transaction.CommitAsync(cancellationToken);
                return attempt.OrderId
                       ?? throw new InvalidOperationException($"Fulfilled attempt '{checkoutAttemptId}' has no order id.");
            }

            if (attempt.Status == CheckoutAttemptStatus.Failed)
                throw new InvalidOperationException($"Checkout attempt '{checkoutAttemptId}' is marked failed.");

            var now = time.GetUtcNow();
            var orderId = Guid.NewGuid();

            var productById = new Dictionary<Guid, Product>();
            foreach (var productId in items.Select(i => i.ProductId).Distinct())
            {
                var product = await products.FindByIdAsync(productId, cancellationToken)
                              ?? throw new InvalidOperationException($"Product '{productId}' for the attempt no longer exists.");
                productById[productId] = product;
            }

            var orderItemEntities = new List<OrderItem>();
            var createdLicences = new List<(Licence Licence, Product Product)>();

            foreach (var item in items)
            {
                var product = productById[item.ProductId];
                for (var unit = 0; unit < item.Quantity; unit++)
                {
                    var labelInput = unit < item.Labels.Count ? item.Labels[unit] : null;
                    var label = string.IsNullOrWhiteSpace(labelInput) ? null : labelInput.Trim();

                    var licence = new Licence(
                        Guid.NewGuid(),
                        item.ProductId,
                        attempt.UserId,
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
                    var mintOutcome = await licenceKeys.MintInTxAsync(
                        connection,
                        transaction,
                        licence.Id,
                        pepperedHmac,
                        keyPrefix,
                        label: null,
                        createdByUserId: null,
                        activeCap: 5,
                        cancellationToken);
                    if (mintOutcome is not MintKeyOutcome.Minted)
                        throw new InvalidOperationException($"Failed to mint initial licence key for licence '{licence.Id}'.");

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

            var order = new Order(orderId, attempt.UserId, attempt.ContactEmail, OrderStatus.Completed, now);
            await orders.CreateInTxAsync(connection, transaction, order, cancellationToken);
            await orderItems.BulkCreateInTxAsync(connection, transaction, orderItemEntities, cancellationToken);

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

            var totals = orderItemEntities
                .GroupBy(i => i.Currency)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new CurrencyTotal(g.Key, g.Sum(i => i.UnitPrice ?? 0m)))
                .ToList();

            var orderPlacedEvt = AuditEvent.Create(
                AuditEventTypes.OrderPlaced,
                AuditSubjectTypes.Order,
                orderId,
                AuditActorTypes.User,
                attempt.UserId,
                reason: null,
                new OrderPlacedPayload(orderItemEntities.Count, totals, attempt.ContactEmail),
                now);
            await auditEvents.RecordInTxAsync(connection, transaction, orderPlacedEvt, cancellationToken);

            foreach (var entry in createdLicences)
            {
                var licenceCreatedEvt = AuditEvent.Create(
                    AuditEventTypes.LicenceCreated,
                    AuditSubjectTypes.Licence,
                    entry.Licence.Id,
                    AuditActorTypes.User,
                    attempt.UserId,
                    reason: null,
                    new LicenceCreatedPayload(orderId, entry.Product.Id, entry.Product.Price, entry.Product.Currency, entry.Licence.Label),
                    now);
                await auditEvents.RecordInTxAsync(connection, transaction, licenceCreatedEvt, cancellationToken);
            }

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

    private static string BuildKeyPrefix(string rawKey)
        => rawKey.Length > 12 ? $"{rawKey[..12]}..." : $"{rawKey}...";
}
