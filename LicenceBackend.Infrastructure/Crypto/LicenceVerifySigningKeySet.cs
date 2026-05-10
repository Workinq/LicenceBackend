using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class LicenceVerifySigningKeySet : IDisposable
{
    private readonly IReadOnlyDictionary<string, ECDsa> _ecdsaByKid;
    private readonly IReadOnlyDictionary<string, ECDsaSecurityKey> _securityKeysByKid;

    public LicenceVerifySigningKeySet(IReadOnlyDictionary<string, ECDsa> ecdsaByKid, string activeKid)
    {
        if (ecdsaByKid.Count == 0) throw new ArgumentException("At least one licence-verify signing key must be configured.", nameof(ecdsaByKid));
        if (!ecdsaByKid.ContainsKey(activeKid))
            throw new ArgumentException(
                $"Active kid '{activeKid}' was not found in the configured licence-verify signing keys.",
                nameof(activeKid));

        _ecdsaByKid = ecdsaByKid;
        _securityKeysByKid = ecdsaByKid.ToDictionary(
            kvp => kvp.Key,
            kvp => new ECDsaSecurityKey(kvp.Value) { KeyId = kvp.Key });
        ActiveKid = activeKid;
    }

    public string ActiveKid { get; }

    public ECDsaSecurityKey ActiveSecurityKey => _securityKeysByKid[ActiveKid];

    public IEnumerable<ECDsaSecurityKey> AllSecurityKeys => _securityKeysByKid.Values;

    public IEnumerable<string> AllKids => _securityKeysByKid.Keys;

    public void Dispose()
    {
        foreach (var ecdsa in _ecdsaByKid.Values) ecdsa.Dispose();
    }
}
