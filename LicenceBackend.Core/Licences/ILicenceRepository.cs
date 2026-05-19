using System.Data;
using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public interface ILicenceRepository
{
    Task<Licence?> FindByKeyHmacAsync(IReadOnlyList<byte[]> keyHmacCandidates, CancellationToken cancellationToken);

    Task<Licence?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(Licence licence, CancellationToken cancellationToken);

    Task CreateInTxAsync(IDbConnection connection, IDbTransaction transaction, Licence licence, CancellationToken cancellationToken);

    Task<Licence?> UpdateLabelAsync(Guid licenceId, Guid ownerId, string? label, CancellationToken cancellationToken);

    Task<PagedResult<Licence>> ListAsync(
        Guid? productId,
        Guid? userId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    Task<PagedResult<Licence>> ListForOwnerAsync(
        Guid ownerId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    Task<PagedResult<UserLicence>> ListForUserAsync(
        Guid userId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );

    Task<Licence?> UpdateStatusAsync(
        Guid licenceId,
        LicenceStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<PinHwidResult> PinHwidAndRecordAttemptAsync(
        Guid licenceId,
        byte[] hwidHmac,
        short hwidHmacPepperVersion,
        Guid productIdRequested,
        string sourceIp,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken
    );

    Task<Licence?> ClearHwidAsync(
        Guid licenceId,
        Guid changedByUserId,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<Licence?> UpdateIpAllowlistAsync(
        Guid licenceId,
        IReadOnlyList<string>? cidrs,
        Guid changedByUserId,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<Licence?> RegenerateKeyAsync(
        Guid licenceId,
        PepperedHmac newKey,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<Licence?> UpdateMaxSeatsAsync(
        Guid licenceId,
        int newMaxSeats,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<IpBindResult> BindFirstUseIpAsync(
        Guid licenceId,
        string hostRoute,
        CancellationToken cancellationToken
    );
}
