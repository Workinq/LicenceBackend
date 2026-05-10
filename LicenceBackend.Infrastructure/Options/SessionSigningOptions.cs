namespace LicenceBackend.Infrastructure.Options;

public sealed class SessionSigningOptions
{
    public const string SectionName = "SessionSigning";

    public IList<SigningKeyEntry> Keys { get; init; } = new List<SigningKeyEntry>();
    public string ActiveKid { get; init; } = string.Empty;
}
