namespace LicenceBackend.Core.Licences;

public sealed record SignedLicenceVerificationClaims(
    Guid LicenceId,
    Guid ProductId,
    string ProductSlug,
    string Status,
    DateTimeOffset? LicenceExpiresAt,
    string? Notes,
    string ClientNonce
);
