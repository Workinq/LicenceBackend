namespace LicenceBackend.Core.Licences;

public sealed record LicenceCheckout(
    Guid Id,
    Guid LicenceId,
    byte[] InstanceIdHash,
    Guid? MemberUserId,
    byte[]? HwidHmac,
    short? HwidHmacPepperVersion,
    string SourceIp,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset ExpiresAt
);
