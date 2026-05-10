using System.Security.Cryptography;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class RefreshTokenGenerator
{
    private const int RawByteLength = 32;

    public string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(RawByteLength);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var encoded = Convert.ToBase64String(bytes);
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
