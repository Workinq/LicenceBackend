namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutClosedPayload(
    Guid CheckoutId,
    string InstanceIdHashPrefix,
    string CloseReason,
    int SeatsAfter,
    int MaxSeats
);
