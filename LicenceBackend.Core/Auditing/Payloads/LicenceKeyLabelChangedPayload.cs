namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceKeyLabelChangedPayload(
    Guid LicenceKeyId,
    string? PreviousLabel,
    string? NewLabel
);
