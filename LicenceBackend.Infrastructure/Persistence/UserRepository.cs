using Dapper;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Users;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class UserRepository(NpgsqlDataSource dataSource, IAuditEventRepository auditEvents, TimeProvider time) : IUserRepository
{
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at
                           FROM users
                           WHERE id = @Id
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);
        return row?.ToDomain();
    }

    public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at
                           FROM users
                           WHERE email_lower = @EmailLower
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { EmailLower = email.Trim().ToLowerInvariant() },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(command);
        return row?.ToDomain();
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        const string sql = "SELECT 1 FROM users WHERE email_lower = @EmailLower LIMIT 1;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { EmailLower = email.Trim().ToLowerInvariant() },
            cancellationToken: cancellationToken);
        var value = await connection.QuerySingleOrDefaultAsync<int?>(command);
        return value.HasValue;
    }

    public async Task CreateAsync(User user, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
                           VALUES (@Id, @Email, @EmailLower, @PasswordHash, @DisplayName, @Role, @Status, @CreatedAt, @UpdatedAt);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                user.Id,
                user.Email,
                user.EmailLower,
                user.PasswordHash,
                user.DisplayName,
                Role = user.Role.ToString().ToLowerInvariant(),
                Status = user.Status.ToString().ToLowerInvariant(),
                user.CreatedAt,
                user.UpdatedAt
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<User>> ListAsync(int limit, int offset, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at
                           FROM users
                           ORDER BY created_at DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM users;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Limit = limit, Offset = offset }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<UserRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<User>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task<User?> UpdateStatusAsync(
        Guid userId,
        UserStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
                                 SELECT id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at
                                 FROM users
                                 WHERE id = @Id
                                 LIMIT 1;
                                 """;

        const string updateSql = """
                                 UPDATE users
                                 SET status = @NewStatus, updated_at = NOW()
                                 WHERE id = @Id
                                 RETURNING id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at;
                                 """;

        const string revokeRefreshesSql = """
                                          UPDATE session_refresh_tokens
                                          SET revoked_at = NOW()
                                          WHERE user_id = @UserId AND revoked_at IS NULL;
                                          """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<UserRow>(
                             new CommandDefinition(selectSql, new { Id = userId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        var currentStatus = Enum.Parse<UserStatus>(currentRow.Status, true);
        if (currentStatus == newStatus) return currentRow.ToDomain();

        var newStatusText = newStatus.ToString().ToLowerInvariant();
        var previousStatusText = currentStatus.ToString().ToLowerInvariant();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<UserRow>(
                                 new CommandDefinition(
                                     updateSql,
                                     new { Id = userId, NewStatus = newStatusText },
                                     transaction,
                                     cancellationToken: cancellationToken));

            var evt = AuditEvent.Create(
                AuditEventTypes.UserStatusChanged,
                AuditSubjectTypes.User,
                userId,
                AuditActorTypes.Admin,
                changedBy,
                reason,
                new UserStatusChangedPayload(previousStatusText, newStatusText),
                time.GetUtcNow()
            );
            await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);

            if (newStatus == UserStatus.Suspended)
                await connection.ExecuteAsync(new CommandDefinition(
                                                  revokeRefreshesSql,
                                                  new { UserId = userId },
                                                  transaction,
                                                  cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed record UserRow(
        Guid Id,
        string Email,
        string EmailLower,
        string PasswordHash,
        string? DisplayName,
        string Role,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        public User ToDomain()
        {
            return new User(
                Id,
                Email,
                EmailLower,
                PasswordHash,
                DisplayName,
                Enum.Parse<UserRole>(Role, true),
                Enum.Parse<UserStatus>(Status, true),
                TimestampConversion.ToUtcOffset(CreatedAt),
                TimestampConversion.ToUtcOffset(UpdatedAt));
        }
    }
}
