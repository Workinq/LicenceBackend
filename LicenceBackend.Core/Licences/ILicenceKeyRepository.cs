using System.Data;

namespace LicenceBackend.Core.Licences;

public sealed record MintLicenceKeyParameters(
    Guid LicenceId,
    PepperedHmac PepperedHmac,
    string KeyPrefix,
    string? Label,
    Guid? CreatedByUserId,
    int ActiveCap
);

public interface ILicenceKeyRepository
{
    Task<LicenceKey?> FindActiveByKeyHmacAsync(IReadOnlyList<byte[]> keyHmacCandidates, CancellationToken cancellationToken);

    Task<LicenceKey?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<LicenceKey>> ListForLicenceAsync(Guid licenceId, bool includeRevoked, CancellationToken cancellationToken);

    Task<int> CountActiveForLicenceAsync(Guid licenceId, CancellationToken cancellationToken);

    Task<MintKeyOutcome> MintAsync(MintLicenceKeyParameters parameters, CancellationToken cancellationToken);

    Task<MintKeyOutcome> MintInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        MintLicenceKeyParameters parameters,
        CancellationToken cancellationToken
    );

    Task<RevokeKeyOutcome> RevokeAsync(
        Guid licenceKeyId,
        Guid revokedByUserId,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<LicenceKey?> UpdateLabelAsync(Guid licenceKeyId, string? newLabel, CancellationToken cancellationToken);

    Task BumpLastSeenAsync(Guid licenceKeyId, DateTimeOffset seenAt, CancellationToken cancellationToken);
}
