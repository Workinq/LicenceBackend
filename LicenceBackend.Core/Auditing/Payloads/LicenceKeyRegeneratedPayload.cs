namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceKeyRegeneratedPayload(
    string? PreviousKeyHmacBase64,
    short? PreviousKeyPepperVersion,
    string NewKeyHmacBase64,
    short NewKeyPepperVersion
);
