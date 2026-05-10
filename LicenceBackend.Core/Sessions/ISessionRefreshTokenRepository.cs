namespace LicenceBackend.Core.Sessions;

public interface ISessionRefreshTokenRepository
{
    Task CreateAsync(SessionRefreshToken token, CancellationToken cancellationToken);

    Task<SessionRefreshToken?> FindByHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>
    ///     Atomically marks <paramref name="oldTokenId" /> revoked with <c>replaced_by = newToken.Id</c>
    ///     and inserts <paramref name="newToken" />, in a single transaction. Returns <c>true</c> if the
    ///     old row was actually revoked by this call (UPDATE affected one row), <c>false</c> if the row
    ///     was already revoked when the UPDATE ran (race loss - caller should treat as reuse).
    /// </summary>
    Task<bool> RotateAsync(
        Guid oldTokenId,
        SessionRefreshToken newToken,
        CancellationToken cancellationToken);

    Task RevokeByIdAsync(Guid tokenId, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
