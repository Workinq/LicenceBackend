using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public interface ILicenceStatusHistoryRepository
{
    Task<PagedResult<LicenceStatusHistoryEntry>> ListForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );
}
