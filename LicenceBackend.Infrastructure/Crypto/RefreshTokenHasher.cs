using System.Security.Cryptography;
using System.Text;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class RefreshTokenHasher
{
    public static byte[] Hash(string rawToken)
    {
        return string.IsNullOrEmpty(rawToken)
                   ? throw new ArgumentException("Refresh token must not be empty.", nameof(rawToken))
                   : SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}
