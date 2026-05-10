namespace LicenceBackend.Core.Licences;

public interface ILicenceKeyHasher
{
    PepperedHmac          HashWithActive(string  licenceKey);
    IReadOnlyList<byte[]> HashAllVersions(string licenceKey);
}

public readonly record struct PepperedHmac(byte[] Hmac, short PepperVersion);
