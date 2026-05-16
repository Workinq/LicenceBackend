namespace LicenceBackend.Core.Licences;

public static class VerificationOutcomeNames
{
    public const string Approved = "approved";
    public const string Denied = "denied";

    public static string ToString(VerificationOutcome outcome)
    {
        return outcome switch
        {
            VerificationOutcome.Approved => Approved,
            VerificationOutcome.Denied => Denied,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }
}

public static class VerificationDenialReasonNames
{
    public const string ProductMismatch = "product_mismatch";
    public const string LicenceNotUsable = "licence_not_usable";
    public const string OwnerSuspended = "owner_suspended";
    public const string IpNotAllowlisted = "ip_not_allowlisted";
    public const string HwidMissing = "hwid_missing";
    public const string HwidMismatch = "hwid_mismatch";

    public static string? ToString(VerificationDenialReason? reason)
    {
        return reason switch
        {
            null => null,
            VerificationDenialReason.ProductMismatch => ProductMismatch,
            VerificationDenialReason.LicenceNotUsable => LicenceNotUsable,
            VerificationDenialReason.OwnerSuspended => OwnerSuspended,
            VerificationDenialReason.IpNotAllowlisted => IpNotAllowlisted,
            VerificationDenialReason.HwidMissing => HwidMissing,
            VerificationDenialReason.HwidMismatch => HwidMismatch,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }
}
