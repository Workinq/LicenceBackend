namespace LicenceBackend.Api.Models.Response;

public sealed record ProductResponse(
    Guid           Id,
    string         Slug,
    string         DisplayName,
    DateTimeOffset CreatedAt
);
