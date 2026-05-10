using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public interface ILicenceBindingHistoryRepository
{
    Task<PagedResult<LicenceBindingHistoryEntry>> ListForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );
}
