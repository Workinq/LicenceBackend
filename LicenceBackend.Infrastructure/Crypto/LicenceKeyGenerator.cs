using System.Security.Cryptography;
using System.Text;
using LicenceBackend.Core.Licences;

namespace LicenceBackend.Infrastructure.Crypto;

public sealed class LicenceKeyGenerator : ILicenceKeyGenerator
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int    GroupSize         = 5;
    private const int    GroupCount        = 5;

    public string Generate()
    {
        Span<byte> random = stackalloc byte[GroupSize * GroupCount];
        RandomNumberGenerator.Fill(random);

        var sb = new StringBuilder("LIC-", 34);
        for (var i = 0; i < random.Length; i++)
        {
            sb.Append(CrockfordAlphabet[random[i] % CrockfordAlphabet.Length]);
            if ((i + 1) % GroupSize == 0 && i != random.Length - 1) sb.Append('-');
        }

        return sb.ToString();
    }
}
