namespace LicenceBackend.Core.Licences;

public sealed record LicenceKey(
    Guid Id,
    Guid LicenceId,
    byte[] KeyHmac,
    short KeyHmacPepperVersion,
    string KeyPrefix,
    string? Label,
    Guid? CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? RevokedAt,
    Guid? RevokedByUserId,
    string? RevokeReason
)
{
    public bool IsActive => RevokedAt is null;
}
