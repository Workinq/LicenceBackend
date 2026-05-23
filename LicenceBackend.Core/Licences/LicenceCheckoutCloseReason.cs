namespace LicenceBackend.Core.Licences;

public enum LicenceCheckoutCloseReason
{
    Checkin,
    Expired,
    AdminRevoked,
    OwnerRevoked,
    KeyRevoked
}
