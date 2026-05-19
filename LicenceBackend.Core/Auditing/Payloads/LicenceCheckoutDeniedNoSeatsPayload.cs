namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutDeniedNoSeatsPayload(
    string InstanceIdHashPrefix,
    string SourceIp,
    int ActiveSeats,
    int MaxSeats,
    DateTimeOffset OldestExpiresAt
);
