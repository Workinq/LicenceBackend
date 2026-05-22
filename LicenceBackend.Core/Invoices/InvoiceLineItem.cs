namespace LicenceBackend.Core.Invoices;

public sealed record InvoiceLineItem(
    Guid Id,
    Guid InvoiceId,
    Guid ProductId,
    Guid LicenceId,
    string ProductName,
    string ProductSlug,
    string? Label,
    decimal? UnitPrice,
    string Currency
);
