namespace LicenceBackend.Core.Licences;

public interface ILicenceMemberRepository
{
    Task AddAsync(LicenceMember member, CancellationToken cancellationToken);

    Task<bool> RemoveAsync(Guid licenceId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LicenceMember>> ListByLicenceAsync(Guid licenceId, CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid licenceId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListLicenceIdsByUserAsync(Guid userId, CancellationToken cancellationToken);
}
