namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceStatusChangedPayload(string PreviousStatus, string NewStatus);
