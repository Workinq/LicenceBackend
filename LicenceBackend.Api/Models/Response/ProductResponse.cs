namespace LicenceBackend.Api.Models.Response;

public sealed record ProductResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? Tagline,
    bool IsPublic,
    decimal? Price,
    string Currency,
    int SortOrder,
    string? ImageUrl,
    DateTimeOffset CreatedAt
);
