namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceKeyResponse(
    Guid Id,
    Guid LicenceId,
    string KeyPrefix,
    string? Label,
    Guid? CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? RevokedAt,
    Guid? RevokedByUserId,
    string? RevokeReason
);
