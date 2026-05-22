using System.Data;
using Dapper;
using LicenceBackend.Core.Invoices;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class InvoiceRepository(NpgsqlDataSource dataSource) : IInvoiceRepository
{
    public async Task<long> CreateInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Invoice invoice,
        IReadOnlyList<InvoiceLineItem> lineItems,
        CancellationToken cancellationToken)
    {
        const string insertInvoiceSql = """
                                        INSERT INTO invoices
                                            (id, order_id, issued_at, contact_email,
                                             buyer_name, buyer_address_line1, buyer_address_line2,
                                             buyer_city, buyer_region, buyer_postal_code, buyer_country)
                                        VALUES
                                            (@Id, @OrderId, @IssuedAt, @ContactEmail,
                                             @BuyerName, @BuyerAddressLine1, @BuyerAddressLine2,
                                             @BuyerCity, @BuyerRegion, @BuyerPostalCode, @BuyerCountry)
                                        RETURNING invoice_number;
                                        """;
        var invoiceCommand = new CommandDefinition(
            insertInvoiceSql,
            new
            {
                invoice.Id,
                invoice.OrderId,
                IssuedAt = invoice.IssuedAt.UtcDateTime,
                invoice.ContactEmail,
                invoice.BuyerName,
                invoice.BuyerAddressLine1,
                invoice.BuyerAddressLine2,
                invoice.BuyerCity,
                invoice.BuyerRegion,
                invoice.BuyerPostalCode,
                invoice.BuyerCountry
            },
            transaction,
            cancellationToken: cancellationToken);
        var invoiceNumber = await connection.ExecuteScalarAsync<long>(invoiceCommand);

        if (lineItems.Count > 0)
        {
            const string insertItemSql = """
                                         INSERT INTO invoice_line_items
                                             (id, invoice_id, product_id, licence_id,
                                              product_name, product_slug, label, unit_price, currency)
                                         VALUES
                                             (@Id, @InvoiceId, @ProductId, @LicenceId,
                                              @ProductName, @ProductSlug, @Label, @UnitPrice, @Currency);
                                         """;
            var itemParams = lineItems.Select(i => new
            {
                i.Id,
                i.InvoiceId,
                i.ProductId,
                i.LicenceId,
                i.ProductName,
                i.ProductSlug,
                i.Label,
                i.UnitPrice,
                i.Currency
            }).ToArray();
            var itemsCommand = new CommandDefinition(insertItemSql, itemParams, transaction, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(itemsCommand);
        }

        return invoiceNumber;
    }

    public async Task<(Invoice Invoice, IReadOnlyList<InvoiceLineItem> LineItems)?> FindByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, order_id, invoice_number, issued_at, contact_email,
                                  buyer_name, buyer_address_line1, buyer_address_line2,
                                  buyer_city, buyer_region, buyer_postal_code, buyer_country
                           FROM invoices
                           WHERE order_id = @OrderId
                           LIMIT 1;

                           SELECT li.id, li.invoice_id, li.product_id, li.licence_id,
                                  li.product_name, li.product_slug, li.label, li.unit_price, li.currency
                           FROM invoice_line_items li
                           JOIN invoices i ON i.id = li.invoice_id
                           WHERE i.order_id = @OrderId
                           ORDER BY li.product_name, li.id;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);

        var invoiceRow = await multi.ReadSingleOrDefaultAsync<InvoiceRow>();
        if (invoiceRow is null) return null;

        var itemRows = (await multi.ReadAsync<InvoiceLineItemRow>()).ToList();
        var lineItems = itemRows.Select(r => r.ToDomain()).ToList();
        return (invoiceRow.ToDomain(), lineItems);
    }

    private sealed record InvoiceRow(
        Guid Id,
        Guid OrderId,
        long InvoiceNumber,
        DateTime IssuedAt,
        string ContactEmail,
        string? BuyerName,
        string? BuyerAddressLine1,
        string? BuyerAddressLine2,
        string? BuyerCity,
        string? BuyerRegion,
        string? BuyerPostalCode,
        string? BuyerCountry
    )
    {
        public Invoice ToDomain() => new(
            Id,
            OrderId,
            InvoiceNumber,
            TimestampConversion.ToUtcOffset(IssuedAt),
            ContactEmail,
            BuyerName,
            BuyerAddressLine1,
            BuyerAddressLine2,
            BuyerCity,
            BuyerRegion,
            BuyerPostalCode,
            BuyerCountry
        );
    }

    private sealed record InvoiceLineItemRow(
        Guid Id,
        Guid InvoiceId,
        Guid ProductId,
        Guid LicenceId,
        string ProductName,
        string ProductSlug,
        string? Label,
        decimal? UnitPrice,
        string Currency
    )
    {
        public InvoiceLineItem ToDomain() => new(
            Id, InvoiceId, ProductId, LicenceId, ProductName, ProductSlug, Label, UnitPrice, Currency);
    }
}
