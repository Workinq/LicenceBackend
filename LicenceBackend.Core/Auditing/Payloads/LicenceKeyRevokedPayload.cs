namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceKeyRevokedPayload(
    Guid LicenceKeyId,
    string KeyHmacBase64,
    string KeyPrefix,
    string? Label,
    int CascadedCheckouts
);
