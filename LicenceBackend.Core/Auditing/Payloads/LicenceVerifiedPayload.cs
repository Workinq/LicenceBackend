namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceVerifiedPayload(
    Guid? ProductIdRequested,
    string? HwidHmacBase64,
    string SourceIp,
    string Outcome,
    string? DenialReason
);
