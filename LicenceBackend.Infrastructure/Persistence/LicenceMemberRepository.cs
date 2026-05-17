using Dapper;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceMemberRepository(NpgsqlDataSource dataSource) : ILicenceMemberRepository
{
    public async Task AddAsync(LicenceMember member, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO licence_members (licence_id, user_id, added_by, added_at)
                           VALUES (@LicenceId, @UserId, @AddedBy, @AddedAt);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                member.LicenceId,
                member.UserId,
                member.AddedBy,
                AddedAt = member.AddedAt.UtcDateTime
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<bool> RemoveAsync(Guid licenceId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM licence_members WHERE licence_id = @LicenceId AND user_id = @UserId;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId, UserId = userId }, cancellationToken: cancellationToken);
        var rows = await connection.ExecuteAsync(command);
        return rows > 0;
    }

    public async Task<IReadOnlyList<LicenceMember>> ListByLicenceAsync(Guid licenceId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT licence_id, user_id, added_by, added_at
                           FROM licence_members
                           WHERE licence_id = @LicenceId
                           ORDER BY added_at ASC;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Row>(command);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<bool> IsMemberAsync(Guid licenceId, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT 1 FROM licence_members WHERE licence_id = @LicenceId AND user_id = @UserId LIMIT 1;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId, UserId = userId }, cancellationToken: cancellationToken);
        var hit = await connection.ExecuteScalarAsync<int?>(command);
        return hit.HasValue;
    }

    public async Task<IReadOnlyList<Guid>> ListLicenceIdsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT licence_id FROM licence_members WHERE user_id = @UserId;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Guid>(command);
        return rows.ToList();
    }

    private sealed record Row(Guid licence_id, Guid user_id, Guid added_by, DateTime added_at)
    {
        public LicenceMember ToDomain() => new(
            licence_id,
            user_id,
            added_by,
            new DateTimeOffset(DateTime.SpecifyKind(added_at, DateTimeKind.Utc))
        );
    }
}
