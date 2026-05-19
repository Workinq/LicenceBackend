namespace LicenceBackend.Core.Auditing;

public static class AuditEventTypes
{
    public const string UserStatusChanged = "user.status_changed";
    public const string UserRoleChanged = "user.role_changed";
    public const string LicenceStatusChanged = "licence.status_changed";
    public const string LicenceBindingChanged = "licence.binding_changed";
    public const string LicenceKeyRegenerated = "licence.key_regenerated";
    public const string LicenceMemberAdded = "licence.member_added";
    public const string LicenceMemberRemoved = "licence.member_removed";
    public const string LicenceVerified = "licence.verified";
    public const string LicenceCreated = "licence.created";
    public const string OrderPlaced = "order.placed";
    public const string ProductFileUploaded = "product.file_uploaded";
    public const string ProductFileDownloaded = "product.file_downloaded";
}
