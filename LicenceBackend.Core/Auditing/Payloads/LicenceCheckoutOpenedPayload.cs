namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutOpenedPayload(
    Guid CheckoutId,
    string InstanceIdHashPrefix,
    Guid? MemberUserId,
    string? HwidHmacBase64,
    string SourceIp,
    int SeatsAfter,
    int MaxSeats
);
