namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record UserRoleChangedPayload(string PreviousRole, string NewRole);
