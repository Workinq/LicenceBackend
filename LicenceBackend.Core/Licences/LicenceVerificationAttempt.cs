namespace LicenceBackend.Core.Licences;

public sealed record LicenceVerificationAttempt(
    Guid                      Id,
    Guid                      LicenceId,
    Guid?                     ProductIdRequested,
    byte[]?                   HwidHmac,
    string                    SourceIp,
    VerificationOutcome       Outcome,
    VerificationDenialReason? DenialReason,
    DateTimeOffset            AttemptedAt
);
