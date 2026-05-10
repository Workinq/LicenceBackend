namespace LicenceBackend.Infrastructure.Crypto;

public sealed class HmacPepperSet
{
    private readonly IReadOnlyDictionary<short, byte[]> _peppersByVersion;

    public HmacPepperSet(IReadOnlyDictionary<short, byte[]> peppersByVersion, short activeVersion)
    {
        if (peppersByVersion.Count == 0) throw new ArgumentException("At least one HMAC pepper must be configured.", nameof(peppersByVersion));
        if (!peppersByVersion.ContainsKey(activeVersion))
            throw new ArgumentException(
                $"Active pepper version '{activeVersion}' was not found in the configured peppers.",
                nameof(activeVersion));
        foreach (var (version, bytes) in peppersByVersion)
            if (bytes.Length < 32)
                throw new ArgumentException(
                    $"Pepper version {version} is shorter than 32 bytes. Regenerate with the dev tools.",
                    nameof(peppersByVersion));

        _peppersByVersion = peppersByVersion;
        ActiveVersion     = activeVersion;
    }

    public short ActiveVersion { get; }

    public byte[] this[short version] =>
        _peppersByVersion.TryGetValue(version, out var pepper)
            ? pepper
            : throw new KeyNotFoundException($"No pepper configured for version {version}.");

    public IEnumerable<short> AllVersions => _peppersByVersion.Keys;

    public bool TryGetValue(short version, out byte[] pepper)
    {
        return _peppersByVersion.TryGetValue(version, out pepper!);
    }
}
