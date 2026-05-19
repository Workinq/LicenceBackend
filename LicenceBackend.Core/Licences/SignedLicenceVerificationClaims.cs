namespace LicenceBackend.Core.Licences;

public sealed record SignedLicenceVerificationClaims(
    Guid LicenceId,
    Guid ProductId,
    string ProductSlug,
    string Status,
    DateTimeOffset? LicenceExpiresAt,
    string? Notes,
    string ClientNonce,
    Guid? SeatId = null,
    DateTimeOffset? SeatExpiresAt = null,
    DateTimeOffset? SeatHeartbeatAfter = null
);
