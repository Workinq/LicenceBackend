namespace LicenceBackend.Core.Users;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string encodedHash);

    void VerifyDummy(string password);
}
