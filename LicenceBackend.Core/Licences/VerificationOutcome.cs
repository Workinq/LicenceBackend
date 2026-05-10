namespace LicenceBackend.Core.Licences;

public enum VerificationOutcome
{
    Approved,
    Denied
}

public enum VerificationDenialReason
{
    ProductMismatch,
    LicenceNotUsable,
    OwnerSuspended,
    IpNotAllowlisted,
    HwidMissing,
    HwidMismatch
}
