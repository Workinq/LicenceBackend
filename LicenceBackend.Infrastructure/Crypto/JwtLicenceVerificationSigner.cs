using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Core.Licences;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class JwtLicenceVerificationSigner : ILicenceVerificationSigner
{
    private const int SignedPayloadTtlSeconds = 60;
    private const string TokenType = "licence-verify+jwt";
    private readonly string _kid;

    private readonly SigningCredentials _signingCredentials;
    private readonly TimeProvider _time;

    public JwtLicenceVerificationSigner(
        LicenceVerifySigningKeySet signingKeySet,
        TimeProvider time)
    {
        _signingCredentials = new SigningCredentials(signingKeySet.ActiveSecurityKey, SecurityAlgorithms.EcdsaSha256);
        _kid = signingKeySet.ActiveKid;
        _time = time;
    }

    public string Sign(SignedLicenceVerificationClaims claims)
    {
        var now = _time.GetUtcNow();
        var expiresAt = now.AddSeconds(SignedPayloadTtlSeconds);

        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("nonce", claims.ClientNonce),
            new("licenceId", claims.LicenceId.ToString()),
            new("productId", claims.ProductId.ToString()),
            new("productSlug", claims.ProductSlug),
            new("status", claims.Status)
        };

        if (claims.LicenceExpiresAt is { } licenceExpiresAt)
            jwtClaims.Add(new Claim("licenceExpiresAt", licenceExpiresAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));

        if (!string.IsNullOrEmpty(claims.Notes)) jwtClaims.Add(new Claim("notes", claims.Notes));

        var jwt = new JwtSecurityToken(
            null,
            null,
            jwtClaims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            _signingCredentials);

        jwt.Header["typ"] = TokenType;
        jwt.Header["kid"] = _kid;

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();
        return handler.WriteToken(jwt);
    }
}
