using LicenceBackend.Core.Auditing;

namespace LicenceBackend.Tests.Unit;

public class AuditEventTypesTests
{
    [Fact]
    public void Event_type_constants_match_canonical_strings()
    {
        Assert.Equal("user.status_changed", AuditEventTypes.UserStatusChanged);
        Assert.Equal("licence.status_changed", AuditEventTypes.LicenceStatusChanged);
        Assert.Equal("licence.binding_changed", AuditEventTypes.LicenceBindingChanged);
        Assert.Equal("licence.key_regenerated", AuditEventTypes.LicenceKeyRegenerated);
        Assert.Equal("licence.verified", AuditEventTypes.LicenceVerified);
    }

    [Fact]
    public void Subject_type_constants_match_canonical_strings()
    {
        Assert.Equal("user", AuditSubjectTypes.User);
        Assert.Equal("licence", AuditSubjectTypes.Licence);
    }

    [Fact]
    public void Actor_type_constants_match_canonical_strings()
    {
        Assert.Equal("admin", AuditActorTypes.Admin);
        Assert.Equal("system", AuditActorTypes.System);
        Assert.Equal("anonymous", AuditActorTypes.Anonymous);
    }

    [Fact]
    public void Event_type_constants_are_unique()
    {
        var values = new[]
        {
            AuditEventTypes.UserStatusChanged,
            AuditEventTypes.LicenceStatusChanged,
            AuditEventTypes.LicenceBindingChanged,
            AuditEventTypes.LicenceKeyRegenerated,
            AuditEventTypes.LicenceVerified
        };
        Assert.Equal(values.Length, values.Distinct().Count());
    }
}
