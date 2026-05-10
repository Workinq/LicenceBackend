namespace LicenceBackend.Core.Products;

public sealed record Product(
    Guid Id,
    string Slug,
    string DisplayName,
    DateTimeOffset CreatedAt
);
