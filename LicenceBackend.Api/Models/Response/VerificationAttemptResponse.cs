namespace LicenceBackend.Api.Models.Response;

public sealed record VerificationAttemptResponse(
    Guid Id,
    Guid LicenceId,
    Guid? ProductIdRequested,
    string? HwidFingerprint,
    string SourceIp,
    string Outcome,
    string? DenialReason,
    DateTimeOffset AttemptedAt
);
