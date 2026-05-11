using System.Net;
using LicenceBackend.Core.Licences;

namespace LicenceBackend.Tests.Core;

public sealed class LicenceIpTests
{
    private static Licence Make(IReadOnlyList<string>? ipAllowlist) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            [1, 2, 3],
            1,
            LicenceStatus.Active,
            null,
            null,
            null,
            null,
            ipAllowlist,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    [Fact]
    public void Null_allowlist_is_not_armed_and_allows_any_ip()
    {
        var licence = Make(null);
        Assert.False(licence.IsIpAutoBindArmed);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("203.0.113.7")));
    }

    [Fact]
    public void Empty_allowlist_is_armed_and_allows_any_ip()
    {
        var licence = Make([]);
        Assert.True(licence.IsIpAutoBindArmed);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("203.0.113.7")));
    }

    [Fact]
    public void Populated_allowlist_is_not_armed_and_enforces_membership()
    {
        var licence = Make(["10.0.0.0/24"]);
        Assert.False(licence.IsIpAutoBindArmed);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("10.0.0.5")));
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("203.0.113.7")));
    }
}
