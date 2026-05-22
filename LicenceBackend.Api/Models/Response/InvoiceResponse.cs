namespace LicenceBackend.Api.Models.Response;

public sealed record InvoiceResponse(
    Guid OrderId,
    string InvoiceNumber,
    DateTimeOffset IssuedAt,
    string Status,
    InvoiceSellerResponse Seller,
    InvoiceBuyerResponse Buyer,
    IReadOnlyList<InvoiceLineItemResponse> LineItems,
    IReadOnlyList<CurrencyTotalResponse> Totals
);

public sealed record InvoiceSellerResponse(
    string Name,
    string AddressLine1,
    string AddressLine2,
    string City,
    string Region,
    string PostalCode,
    string Country
);

public sealed record InvoiceBuyerResponse(
    string ContactEmail,
    string? Name,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Region,
    string? PostalCode,
    string? Country
);

public sealed record InvoiceLineItemResponse(
    Guid LicenceId,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string? Label,
    decimal? UnitPrice,
    string Currency
);
