using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using LicenceBackend.Core.Licences;
using LicenceBackend.Infrastructure.Crypto;
using Microsoft.Extensions.Time.Testing;

namespace LicenceBackend.Tests.Unit;

public sealed class SignedLicenceVerificationClaimsTests
{
    [Fact]
    public void Seat_fields_default_to_null()
    {
        var claims = new SignedLicenceVerificationClaims(
            Guid.NewGuid(), Guid.NewGuid(), "p", "active", null, null, "nonce-abc");

        Assert.Null(claims.SeatId);
        Assert.Null(claims.SeatExpiresAt);
        Assert.Null(claims.SeatHeartbeatAfter);
    }
}

public sealed class JwtLicenceVerificationSignerSeatTests
{
    private static (LicenceVerifySigningKeySet set, ECDsa ecdsa) BuildKeySet()
    {
        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keys = new Dictionary<string, ECDsa> { ["test-kid"] = ecdsa };
        var set = new LicenceVerifySigningKeySet(keys, "test-kid");
        return (set, ecdsa);
    }

    [Fact]
    public void Sign_emits_seat_claims_when_present()
    {
        var (set, _) = BuildKeySet();
        using var _set = set;

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var signer = new JwtLicenceVerificationSigner(set, time);

        var seatExpiresAt = time.GetUtcNow().AddMinutes(10);
        var seatHeartbeatAfter = time.GetUtcNow().AddMinutes(5);
        var seatId = Guid.NewGuid();

        var token = signer.Sign(new SignedLicenceVerificationClaims(
            Guid.NewGuid(), Guid.NewGuid(), "prod", "active", null, null,
            "nonce-xyz", seatId, seatExpiresAt, seatHeartbeatAfter));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(seatId.ToString(), jwt.Claims.Single(c => c.Type == "seatId").Value);
        Assert.Equal(seatExpiresAt.ToUnixTimeSeconds().ToString(), jwt.Claims.Single(c => c.Type == "seatExpiresAt").Value);
        Assert.Equal(seatHeartbeatAfter.ToUnixTimeSeconds().ToString(), jwt.Claims.Single(c => c.Type == "seatHeartbeatAfter").Value);
    }

    [Fact]
    public void Sign_omits_seat_claims_when_null()
    {
        var (set, _) = BuildKeySet();
        using var _set = set;

        var time = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var signer = new JwtLicenceVerificationSigner(set, time);

        var token = signer.Sign(new SignedLicenceVerificationClaims(
            Guid.NewGuid(), Guid.NewGuid(), "prod", "active", null, null, "nonce-xyz"));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "seatId");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "seatExpiresAt");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "seatHeartbeatAfter");
    }
}
