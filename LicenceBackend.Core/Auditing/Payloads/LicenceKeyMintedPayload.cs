namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceKeyMintedPayload(
    Guid LicenceKeyId,
    string KeyHmacBase64,
    short KeyPepperVersion,
    string KeyPrefix,
    string? Label
);
