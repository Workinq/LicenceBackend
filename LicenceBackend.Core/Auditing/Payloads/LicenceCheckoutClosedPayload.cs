namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutClosedPayload(
    Guid CheckoutId,
    string InstanceIdHashPrefix,
    Guid? MemberUserId,
    string? HwidHmacBase64,
    string SourceIp,
    string CloseReason,
    int SeatsAfter,
    int MaxSeats
);
