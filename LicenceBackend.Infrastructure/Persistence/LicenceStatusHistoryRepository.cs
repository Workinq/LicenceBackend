using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceStatusHistoryRepository(NpgsqlDataSource dataSource) : ILicenceStatusHistoryRepository
{
    public async Task<PagedResult<LicenceStatusHistoryEntry>> ListForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, licence_id, previous_status, new_status, changed_by, changed_at, reason
                           FROM licence_status_history
                           WHERE licence_id = @LicenceId
                           ORDER BY changed_at DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM licence_status_history
                           WHERE licence_id = @LicenceId;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { LicenceId = licenceId, Limit = limit, Offset = offset },
            cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<HistoryRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<LicenceStatusHistoryEntry>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private sealed record HistoryRow(
        Guid Id,
        Guid LicenceId,
        string PreviousStatus,
        string NewStatus,
        Guid ChangedBy,
        DateTime ChangedAt,
        string? Reason
    )
    {
        public LicenceStatusHistoryEntry ToDomain()
        {
            return new LicenceStatusHistoryEntry(
                Id,
                LicenceId,
                Enum.Parse<LicenceStatus>(PreviousStatus, true),
                Enum.Parse<LicenceStatus>(NewStatus, true),
                ChangedBy,
                TimestampConversion.ToUtcOffset(ChangedAt),
                Reason);
        }
    }
}
