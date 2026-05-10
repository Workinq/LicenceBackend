using System.Net;
using LicenceBackend.Core.Licences;

namespace LicenceBackend.Tests.Unit;

public sealed class IpAllowlistTests
{
    [Fact]
    public void Null_allowlist_allows_every_address()
    {
        var licence = LicenceWithAllowlist(null);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("203.0.113.10")));
        Assert.True(licence.IsIpAllowed(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void Ipv4_cidr_match_inside_range()
    {
        var licence = LicenceWithAllowlist(["10.0.0.0/24"]);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("10.0.0.5")));
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("10.0.1.5")));
    }

    [Fact]
    public void Ipv6_cidr_match()
    {
        var licence = LicenceWithAllowlist(["2001:db8::/32"]);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("2001:db8:1::1")));
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("2001:db9::1")));
    }

    [Fact]
    public void Single_host_cidr_v4_and_v6()
    {
        var licence = LicenceWithAllowlist(["203.0.113.7/32", "::1/128"]);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("203.0.113.7")));
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("203.0.113.8")));
        Assert.True(licence.IsIpAllowed(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void Empty_allowlist_blocks_every_address()
    {
        var licence = LicenceWithAllowlist([]);
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("203.0.113.10")));
        Assert.False(licence.IsIpAllowed(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void Malformed_cidr_is_skipped_not_thrown()
    {
        var licence = LicenceWithAllowlist(["not-a-cidr", "10.0.0.0/24"]);
        Assert.True(licence.IsIpAllowed(IPAddress.Parse("10.0.0.5")));
        Assert.False(licence.IsIpAllowed(IPAddress.Parse("203.0.113.10")));
    }

    private static Licence LicenceWithAllowlist(IReadOnlyList<string>? allowlist)
    {
        return new Licence(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new byte[32],
            1,
            LicenceStatus.Active,
            null,
            null,
            null,
            null,
            allowlist,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
