using System.Net;
using System.Security.Cryptography;

namespace LicenceBackend.Core.Licences;

public sealed record Licence(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    byte[] KeyHmac,
    short KeyHmacPepperVersion,
    LicenceStatus Status,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    byte[]? HwidHmac,
    short? HwidHmacPepperVersion,
    IReadOnlyList<string>? IpAllowlist,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
)
{
    public bool IsUsableAt(DateTimeOffset now)
    {
        return Status == LicenceStatus.Active && (ExpiresAt is null || ExpiresAt > now);
    }

    public bool IsHwidAllowed(byte[]? presentedHwidHmac)
    {
        if (HwidHmac is null) return true;

        if (presentedHwidHmac is null) return false;

        return CryptographicOperations.FixedTimeEquals(HwidHmac, presentedHwidHmac);
    }

    public bool IsIpAutoBindArmed => IpAllowlist is { Count: 0 };

    public bool IsIpAllowed(IPAddress remote)
    {
        if (IpAllowlist is null || IpAllowlist.Count == 0) return true;

        foreach (var cidr in IpAllowlist)
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(remote))
                return true;

        return false;
    }
}
