namespace LicenceBackend.Core.Licences;

public interface IHwidHasher
{
    PepperedHmac HashWithActive(string hwid);

    bool TryHashWithVersion(string hwid, short pepperVersion, out byte[] hmac);
}
