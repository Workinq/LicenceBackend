namespace LicenceBackend.Infrastructure.Options;

public sealed class LicenceVerifySigningOptions
{
    public const string SectionName = "LicenceVerifySigning";

    public IList<SigningKeyEntry> Keys      { get; init; } = new List<SigningKeyEntry>();
    public string                 ActiveKid { get; init; } = string.Empty;
}
