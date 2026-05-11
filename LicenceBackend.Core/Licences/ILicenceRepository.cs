using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public interface ILicenceRepository
{
    Task<Licence?> FindByKeyHmacAsync(IReadOnlyList<byte[]> keyHmacCandidates, CancellationToken cancellationToken);

    Task<Licence?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task CreateAsync(Licence licence, CancellationToken cancellationToken);

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
        string sourceIp,
        LicenceVerificationAttempt approvedAttempt,
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

    Task<IpBindResult> BindFirstUseIpAsync(
        Guid licenceId,
        string hostRoute,
        CancellationToken cancellationToken
    );
}
