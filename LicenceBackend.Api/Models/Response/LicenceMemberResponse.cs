namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceMemberResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    Guid AddedBy,
    string? AddedByEmail,
    DateTimeOffset AddedAt
);
