namespace LicenceBackend.Core.Auditing;

public static class AuditEventTypes
{
    public const string UserStatusChanged = "user.status_changed";
    public const string LicenceStatusChanged = "licence.status_changed";
    public const string LicenceBindingChanged = "licence.binding_changed";
    public const string LicenceKeyRegenerated = "licence.key_regenerated";
    public const string LicenceVerified = "licence.verified";
}
