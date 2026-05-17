using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Users;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);

    Task CreateAsync(User user, CancellationToken cancellationToken);

    Task<PagedResult<User>> ListAsync(
        int limit,
        int offset,
        string? q,
        UserRole? role,
        UserStatus? status,
        CancellationToken cancellationToken
    );

    Task<User?> UpdateStatusAsync(
        Guid userId,
        UserStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken
    );

    Task<User?> UpdateDisplayNameAsync(
        Guid userId,
        string? displayName,
        CancellationToken cancellationToken
    );

    Task<User?> UpdatePasswordAsync(
        Guid userId,
        string newPasswordHash,
        Guid? keepRefreshTokenId,
        CancellationToken cancellationToken
    );
}
