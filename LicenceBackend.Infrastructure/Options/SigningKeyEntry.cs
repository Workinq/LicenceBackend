namespace LicenceBackend.Infrastructure.Options;

public sealed class SigningKeyEntry
{
    public string Kid            { get; init; } = string.Empty;
    public string PrivateKeyPath { get; init; } = string.Empty;
}
