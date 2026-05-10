using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LicenceBackend.Core.Sessions;
using LicenceBackend.Core.Users;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class JwtSessionTokenIssuer : ISessionTokenIssuer
{
    private readonly SessionOptions     _sessionOptions;
    private readonly SigningCredentials _signingCredentials;
    private readonly TimeProvider       _time;

    public JwtSessionTokenIssuer(
        SessionSigningKeySet     signingKeySet,
        IOptions<SessionOptions> sessionOptions,
        TimeProvider             time)
    {
        _signingCredentials = new SigningCredentials(signingKeySet.ActiveSecurityKey, SecurityAlgorithms.EcdsaSha256);
        _sessionOptions     = sessionOptions.Value;
        _time               = time;
    }

    public SessionToken Issue(User user, Guid sessionId)
    {
        var now       = _time.GetUtcNow();
        var expiresAt = now.AddSeconds(_sessionOptions.TtlSeconds);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role.ToString().ToLowerInvariant()),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var jwt = new JwtSecurityToken(
            _sessionOptions.Issuer,
            _sessionOptions.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            _signingCredentials);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();
        var token = handler.WriteToken(jwt);

        return new SessionToken(token, expiresAt);
    }
}
