using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceBindingHistoryRepository(NpgsqlDataSource dataSource) : ILicenceBindingHistoryRepository
{
    public async Task<PagedResult<LicenceBindingHistoryEntry>> ListForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken
    )
    {
        const string sql = """
                           SELECT id, licence_id, binding_type, previous_value, new_value,
                                  change_source, changed_by_user_id, changed_at, reason
                           FROM licence_binding_history
                           WHERE licence_id = @LicenceId
                           ORDER BY changed_at DESC, id DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM licence_binding_history
                           WHERE licence_id = @LicenceId;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId, Limit = limit, Offset = offset }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<Row>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<LicenceBindingHistoryEntry>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private sealed record Row(
        Guid Id,
        Guid LicenceId,
        string BindingType,
        string? PreviousValue,
        string? NewValue,
        string ChangeSource,
        Guid? ChangedByUserId,
        DateTime ChangedAt,
        string? Reason
    )
    {
        public LicenceBindingHistoryEntry ToDomain()
        {
            return new LicenceBindingHistoryEntry(
                Id,
                LicenceId,
                ParseBindingType(BindingType),
                PreviousValue,
                NewValue,
                ParseChangeSource(ChangeSource),
                ChangedByUserId,
                TimestampConversion.ToUtcOffset(ChangedAt),
                Reason);
        }

        private static LicenceBindingType ParseBindingType(string value)
        {
            return value switch
            {
                "hwid" => LicenceBindingType.Hwid,
                "ip_allowlist" => LicenceBindingType.IpAllowlist,
                _ => throw new InvalidOperationException($"Unknown binding_type '{value}'.")
            };
        }

        private static BindingChangeSource ParseChangeSource(string value)
        {
            return value switch
            {
                "admin" => BindingChangeSource.Admin,
                "first_use" => BindingChangeSource.FirstUse,
                _ => throw new InvalidOperationException($"Unknown change_source '{value}'.")
            };
        }
    }
}
