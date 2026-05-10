namespace LicenceBackend.Core.Sessions;

public sealed record SessionRefreshToken(
    Guid            Id,
    Guid            UserId,
    byte[]          TokenHash,
    DateTimeOffset  IssuedAt,
    DateTimeOffset  ExpiresAt,
    DateTimeOffset? RevokedAt,
    Guid?           ReplacedBy
);
