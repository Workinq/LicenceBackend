using System.Security.Cryptography;
using System.Text;
using LicenceBackend.Core.Licences;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class HmacLicenceKeyHasher : ILicenceKeyHasher
{
    private readonly HmacPepperSet _pepperSet;

    public HmacLicenceKeyHasher(HmacPepperSet pepperSet)
    {
        _pepperSet = pepperSet;
    }

    public PepperedHmac HashWithActive(string licenceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenceKey);

        var bytes = Encoding.UTF8.GetBytes(licenceKey.Trim());
        var version = _pepperSet.ActiveVersion;
        var hmac = HMACSHA256.HashData(_pepperSet[version], bytes);
        return new PepperedHmac(hmac, version);
    }

    public IReadOnlyList<byte[]> HashAllVersions(string licenceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenceKey);

        var bytes = Encoding.UTF8.GetBytes(licenceKey.Trim());
        // Active version first so the index seek resolves it earliest in normal operation
        // (most rows hit on the active pepper post-rotation).
        var ordered = new[] { _pepperSet.ActiveVersion }
                      .Concat(_pepperSet.AllVersions.Where(v => v != _pepperSet.ActiveVersion))
                      .ToArray();

        var hashes = new byte[ordered.Length][];
        for (var i = 0; i < ordered.Length; i++) hashes[i] = HMACSHA256.HashData(_pepperSet[ordered[i]], bytes);
        return hashes;
    }
}
