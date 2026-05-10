using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Users;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task CreateAsync(User user, CancellationToken cancellationToken);

    Task<PagedResult<User>> ListAsync(int limit, int offset, CancellationToken cancellationToken);

    Task<User?> UpdateStatusAsync(
        Guid userId,
        UserStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken
    );
}
