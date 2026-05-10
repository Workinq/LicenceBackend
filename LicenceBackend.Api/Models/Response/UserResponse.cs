namespace LicenceBackend.Api.Models.Response;

public sealed record UserResponse(
    Guid           Id,
    string         Email,
    string?        DisplayName,
    string         Role,
    string         Status,
    DateTimeOffset CreatedAt
);
