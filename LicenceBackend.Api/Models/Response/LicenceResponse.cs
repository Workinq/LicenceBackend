namespace LicenceBackend.Api.Models.Response;

public record LicenceResponse(
    Guid Id,
    Guid ProductId,
    string ProductSlug,
    Guid UserId,
    string UserEmail,
    string Status,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    bool HwidBound,
    IReadOnlyList<string>? IpAllowlist,
    string? Label,
    DateTimeOffset CreatedAt,
    string? Relationship = null
);

public sealed record LicenceCreatedResponse(
    Guid Id,
    Guid ProductId,
    string ProductSlug,
    Guid UserId,
    string UserEmail,
    string Status,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    bool HwidBound,
    IReadOnlyList<string>? IpAllowlist,
    string? Label,
    DateTimeOffset CreatedAt,
    string LicenceKey
) : LicenceResponse(Id, ProductId, ProductSlug, UserId, UserEmail, Status, ExpiresAt, Notes, HwidBound, IpAllowlist, Label, CreatedAt, null);
