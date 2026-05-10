namespace LicenceBackend.Infrastructure.Options;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int TtlSeconds { get; init; } = 900;
    public int RefreshTtlSeconds { get; init; } = 2_592_000;
}
