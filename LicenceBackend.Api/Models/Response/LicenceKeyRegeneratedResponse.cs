namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceKeyRegeneratedResponse(
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
    DateTimeOffset CreatedAt,
    string LicenceKey
) : LicenceResponse(Id, ProductId, ProductSlug, UserId, UserEmail, Status, ExpiresAt, Notes, HwidBound, IpAllowlist, CreatedAt);
