namespace LicenceBackend.Api;

internal static class ProblemTitles
{
    public const string AccountSuspended = "account_suspended";
    public const string AmbiguousOwner = "ambiguous_owner";
    public const string CannotSuspendSelf = "cannot_suspend_self";
    public const string EmailAlreadyExists = "email_already_exists";
    public const string InvalidCredentials = "invalid_credentials";
    public const string InvalidExpiresAt = "invalid_expires_at";
    public const string InvalidHwid = "invalid_hwid";
    public const string InvalidIpAllowlist = "invalid_ip_allowlist";
    public const string InvalidLicence = "invalid_licence";
    public const string InvalidOutcome = "invalid_outcome";
    public const string InvalidProductImage = "invalid_product_image";
    public const string InvalidRefresh = "invalid_refresh";
    public const string InvalidRole = "invalid_role";
    public const string InvalidStatus = "invalid_status";
    public const string InvalidSubjectType = "invalid_subject_type";
    public const string LicenceNotFound = "licence_not_found";
    public const string MemberAlreadyExists = "member_already_exists";
    public const string MemberIsOwner = "member_is_owner";
    public const string MissingOwner = "missing_owner";
    public const string OwnerNotFound = "owner_not_found";
    public const string ProductNotFound = "product_not_found";
    public const string SlugAlreadyExists = "slug_already_exists";
    public const string UserNotFound = "user_not_found";
}
