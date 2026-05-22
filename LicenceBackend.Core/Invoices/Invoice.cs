namespace LicenceBackend.Core.Invoices;

public sealed record Invoice(
    Guid Id,
    Guid OrderId,
    long InvoiceNumber,
    DateTimeOffset IssuedAt,
    string ContactEmail,
    string? BuyerName,
    string? BuyerAddressLine1,
    string? BuyerAddressLine2,
    string? BuyerCity,
    string? BuyerRegion,
    string? BuyerPostalCode,
    string? BuyerCountry
);
