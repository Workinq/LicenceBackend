namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceMemberChangedPayload(Guid MemberUserId, string MemberEmail);
