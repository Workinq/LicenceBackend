using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Users;

public interface IUserStatusHistoryRepository
{
    Task<PagedResult<UserStatusHistoryEntry>> ListForUserAsync(
        Guid              userId,
        int               limit,
        int               offset,
        CancellationToken cancellationToken);
}
