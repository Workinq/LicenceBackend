namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record UserStatusChangedPayload(string PreviousStatus, string NewStatus);
