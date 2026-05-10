using System.Security.Cryptography;
using System.Text;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class RefreshTokenHasher
{
    public byte[] Hash(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken)) throw new ArgumentException("Refresh token must not be empty.", nameof(rawToken));

        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}
