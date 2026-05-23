namespace LicenceBackend.Core.Licences;

public abstract record MintKeyOutcome
{
    public sealed record Minted(LicenceKey Key) : MintKeyOutcome;
    public sealed record LicenceNotFound : MintKeyOutcome;
    public sealed record CapExceeded(int ActiveCount, int Cap) : MintKeyOutcome;
}
