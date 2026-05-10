using System.Security.Cryptography;
using System.Text;
using LicenceBackend.Core.Licences;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class HmacHwidHasher : IHwidHasher
{
    private readonly HmacPepperSet _pepperSet;

    public HmacHwidHasher(HmacPepperSet pepperSet)
    {
        _pepperSet = pepperSet;
    }

    public PepperedHmac HashWithActive(string hwid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hwid);

        var bytes   = Encoding.UTF8.GetBytes(hwid.Trim());
        var version = _pepperSet.ActiveVersion;
        var hmac    = HMACSHA256.HashData(_pepperSet[version], bytes);
        return new PepperedHmac(hmac, version);
    }

    public bool TryHashWithVersion(string hwid, short pepperVersion, out byte[] hmac)
    {
        if (string.IsNullOrWhiteSpace(hwid) || !_pepperSet.TryGetValue(pepperVersion, out var pepper))
        {
            hmac = [];
            return false;
        }

        var bytes = Encoding.UTF8.GetBytes(hwid.Trim());
        hmac = HMACSHA256.HashData(pepper, bytes);
        return true;
    }
}
