using System.Security.Cryptography;

namespace LicenceBackend.Infrastructure.Crypto;

public static class EcdsaKeyLoader
{
    public static ECDsa LoadFromPemFile(string pemPath)
    {
        if (!File.Exists(pemPath))
            throw new FileNotFoundException(
                $"Signing key PEM not found at '{pemPath}'. Generate one with the dev tools.",
                pemPath);

        var pem   = File.ReadAllText(pemPath);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);

        if (ecdsa.KeySize != 256)
        {
            ecdsa.Dispose();
            throw new InvalidOperationException(
                $"Expected an ECDSA P-256 (256-bit) key, got {ecdsa.KeySize} bits.");
        }

        return ecdsa;
    }
}
