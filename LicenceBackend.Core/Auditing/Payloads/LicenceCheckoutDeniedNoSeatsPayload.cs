namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutDeniedNoSeatsPayload(
    string InstanceIdHashPrefix,
    Guid? MemberUserId,
    string? HwidHmacBase64,
    string SourceIp,
    int ActiveSeats,
    int MaxSeats,
    DateTimeOffset OldestExpiresAt
);
