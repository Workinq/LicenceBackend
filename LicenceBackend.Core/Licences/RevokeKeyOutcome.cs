namespace LicenceBackend.Core.Licences;

public abstract record RevokeKeyOutcome
{
    public sealed record Revoked(LicenceKey Key, int CascadedCheckouts) : RevokeKeyOutcome;
    public sealed record NotFound : RevokeKeyOutcome;
    public sealed record AlreadyRevoked(LicenceKey Key) : RevokeKeyOutcome;
}
