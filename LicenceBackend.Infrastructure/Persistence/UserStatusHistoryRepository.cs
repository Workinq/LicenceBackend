using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Users;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class UserStatusHistoryRepository(NpgsqlDataSource dataSource) : IUserStatusHistoryRepository
{
    public async Task<PagedResult<UserStatusHistoryEntry>> ListForUserAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, user_id, previous_status, new_status, changed_by, changed_at, reason
                           FROM user_status_history
                           WHERE user_id = @UserId
                           ORDER BY changed_at DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM user_status_history
                           WHERE user_id = @UserId;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, Limit = limit, Offset = offset },
            cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<HistoryRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<UserStatusHistoryEntry>(rows.Select(row => row.ToDomain()).ToList(), total);
    }

    private sealed record HistoryRow(
        Guid Id,
        Guid UserId,
        string PreviousStatus,
        string NewStatus,
        Guid ChangedBy,
        DateTime ChangedAt,
        string? Reason
    )
    {
        public UserStatusHistoryEntry ToDomain()
        {
            return new UserStatusHistoryEntry(
                Id,
                UserId,
                Enum.Parse<UserStatus>(PreviousStatus, true),
                Enum.Parse<UserStatus>(NewStatus, true),
                ChangedBy,
                TimestampConversion.ToUtcOffset(ChangedAt),
                Reason);
        }
    }
}
