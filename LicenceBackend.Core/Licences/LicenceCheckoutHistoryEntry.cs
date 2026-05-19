namespace LicenceBackend.Core.Licences;

public sealed record LicenceCheckoutHistoryEntry(
    Guid Id,
    Guid LicenceId,
    Guid CheckoutId,
    byte[] InstanceIdHash,
    Guid? MemberUserId,
    byte[]? HwidHmac,
    string SourceIp,
    DateTimeOffset IssuedAt,
    DateTimeOffset ClosedAt,
    LicenceCheckoutCloseReason CloseReason
);
