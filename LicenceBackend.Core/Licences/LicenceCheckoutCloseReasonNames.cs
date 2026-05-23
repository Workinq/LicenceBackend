namespace LicenceBackend.Core.Licences;

public static class LicenceCheckoutCloseReasonNames
{
    public const string Checkin = "checkin";
    public const string Expired = "expired";
    public const string AdminRevoked = "admin_revoked";
    public const string OwnerRevoked = "owner_revoked";
    public const string KeyRevoked = "key_revoked";

    public static string ToString(LicenceCheckoutCloseReason reason)
    {
        return reason switch
        {
            LicenceCheckoutCloseReason.Checkin => Checkin,
            LicenceCheckoutCloseReason.Expired => Expired,
            LicenceCheckoutCloseReason.AdminRevoked => AdminRevoked,
            LicenceCheckoutCloseReason.OwnerRevoked => OwnerRevoked,
            LicenceCheckoutCloseReason.KeyRevoked => KeyRevoked,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }

    public static LicenceCheckoutCloseReason Parse(string reason)
    {
        return reason switch
        {
            Checkin => LicenceCheckoutCloseReason.Checkin,
            Expired => LicenceCheckoutCloseReason.Expired,
            AdminRevoked => LicenceCheckoutCloseReason.AdminRevoked,
            OwnerRevoked => LicenceCheckoutCloseReason.OwnerRevoked,
            KeyRevoked => LicenceCheckoutCloseReason.KeyRevoked,
            _ => throw new ArgumentException($"Unknown licence checkout close reason '{reason}'.", nameof(reason))
        };
    }
}
