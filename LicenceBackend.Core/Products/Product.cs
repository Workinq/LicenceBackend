using System.Text.Json;

namespace LicenceBackend.Core.Products;

public sealed record Product(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? Tagline,
    bool IsPublic,
    decimal? Price,
    string Currency,
    int SortOrder,
    string? ImagePath,
    string? ImageContentType,
    DateTimeOffset CreatedAt,
    JsonElement? PageContent = null
);
