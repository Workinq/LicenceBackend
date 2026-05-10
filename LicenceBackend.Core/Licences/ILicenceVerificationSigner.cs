namespace LicenceBackend.Core.Licences;

public interface ILicenceVerificationSigner
{
    string Sign(SignedLicenceVerificationClaims claims);
}
