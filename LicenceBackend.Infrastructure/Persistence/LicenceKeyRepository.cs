using Dapper;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceKeyRepository(NpgsqlDataSource dataSource) : ILicenceKeyRepository
{
    private const string Columns =
        "id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, label, created_by_user_id, created_at, last_seen_at, revoked_at, revoked_by_user_id, revoke_reason";

    public async Task<LicenceKey?> FindActiveByKeyHmacAsync(IReadOnlyList<byte[]> keyHmacCandidates, CancellationToken cancellationToken)
    {
        if (keyHmacCandidates.Count == 0) return null;

        const string sql = $"""
                            SELECT {Columns}
                            FROM licence_keys
                            WHERE key_hmac = ANY(@Hashes) AND revoked_at IS NULL
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Hashes = keyHmacCandidates.ToArray() },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(command);
        return row?.ToDomain();
    }

    public async Task<LicenceKey?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM licence_keys
                            WHERE id = @Id
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(command);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<LicenceKey>> ListForLicenceAsync(Guid licenceId, bool includeRevoked, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM licence_keys
                            WHERE licence_id = @LicenceId
                              AND (@IncludeRevoked OR revoked_at IS NULL)
                            ORDER BY (revoked_at IS NULL) DESC, created_at DESC;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { LicenceId = licenceId, IncludeRevoked = includeRevoked },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Row>(command);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<int> CountActiveForLicenceAsync(Guid licenceId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM licence_keys WHERE licence_id = @LicenceId AND revoked_at IS NULL;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task<MintKeyOutcome> MintAsync(
        Guid licenceId,
        PepperedHmac pepperedHmac,
        string keyPrefix,
        string? label,
        Guid? createdByUserId,
        int activeCap,
        CancellationToken cancellationToken)
    {
        const string licenceExistsSql = "SELECT 1 FROM licences WHERE id = @LicenceId LIMIT 1;";
        const string countSql = "SELECT COUNT(*) FROM licence_keys WHERE licence_id = @LicenceId AND revoked_at IS NULL;";
        const string advisoryLockSql = "SELECT pg_advisory_xact_lock(hashtextextended('licence_keys_mint:' || @LicenceId::text, 0));";
        const string insertSql = $"""
                                  INSERT INTO licence_keys (id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, label, created_by_user_id, created_at)
                                  VALUES (@Id, @LicenceId, @KeyHmac, @PepperVersion, @KeyPrefix, @Label, @CreatedByUserId, NOW())
                                  RETURNING {Columns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(advisoryLockSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));

            var licenceExists = await connection.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(licenceExistsSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
            if (licenceExists is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new MintKeyOutcome.LicenceNotFound();
            }

            var activeCount = await connection.QuerySingleAsync<int>(
                new CommandDefinition(countSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
            if (activeCount >= activeCap)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new MintKeyOutcome.CapExceeded(activeCount, activeCap);
            }

            var inserted = await connection.QuerySingleAsync<Row>(
                new CommandDefinition(
                    insertSql,
                    new
                    {
                        Id = Guid.NewGuid(),
                        LicenceId = licenceId,
                        KeyHmac = pepperedHmac.Hmac,
                        PepperVersion = pepperedHmac.PepperVersion,
                        KeyPrefix = keyPrefix,
                        Label = label,
                        CreatedByUserId = createdByUserId
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new MintKeyOutcome.Minted(inserted.ToDomain());
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<RevokeKeyOutcome> RevokeAsync(
        Guid licenceKeyId,
        Guid revokedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string updateSql = $"""
                                  UPDATE licence_keys
                                  SET revoked_at = NOW(), revoked_by_user_id = @ActorId, revoke_reason = @Reason
                                  WHERE id = @Id AND revoked_at IS NULL
                                  RETURNING {Columns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var updated = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(updateSql, new { Id = licenceKeyId, ActorId = revokedByUserId, Reason = reason }, cancellationToken: cancellationToken));

        if (updated is not null) return new RevokeKeyOutcome.Revoked(updated.ToDomain(), CascadedCheckouts: 0);

        var existing = await FindByIdAsync(licenceKeyId, cancellationToken);
        return existing is null ? new RevokeKeyOutcome.NotFound() : new RevokeKeyOutcome.AlreadyRevoked(existing);
    }

    public async Task<LicenceKey?> UpdateLabelAsync(Guid licenceKeyId, string? newLabel, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            UPDATE licence_keys
                            SET label = @Label
                            WHERE id = @Id
                            RETURNING {Columns};
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = licenceKeyId, Label = newLabel },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(command);
        return row?.ToDomain();
    }

    public async Task BumpLastSeenAsync(Guid licenceKeyId, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE licence_keys
                           SET last_seen_at = @SeenAt
                           WHERE id = @Id AND revoked_at IS NULL;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = licenceKeyId, SeenAt = seenAt },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private sealed record Row(
        Guid Id,
        Guid LicenceId,
        byte[] KeyHmac,
        short KeyHmacPepperVersion,
        string KeyPrefix,
        string? Label,
        Guid? CreatedByUserId,
        DateTime CreatedAt,
        DateTime? LastSeenAt,
        DateTime? RevokedAt,
        Guid? RevokedByUserId,
        string? RevokeReason
    )
    {
        public LicenceKey ToDomain() => new(
            Id,
            LicenceId,
            KeyHmac,
            KeyHmacPepperVersion,
            KeyPrefix,
            Label,
            CreatedByUserId,
            TimestampConversion.ToUtcOffset(CreatedAt),
            TimestampConversion.ToUtcOffset(LastSeenAt),
            TimestampConversion.ToUtcOffset(RevokedAt),
            RevokedByUserId,
            RevokeReason);
    }
}
