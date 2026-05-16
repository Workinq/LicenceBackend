using System.Data;
using System.Text.Json;
using Dapper;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceRepository(NpgsqlDataSource dataSource, IAuditEventRepository auditEvents, TimeProvider time) : ILicenceRepository
{
    private const string LicenceColumns =
        "id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, expires_at, notes, hwid_hmac, hwid_hmac_pepper_version, ip_allowlist, created_at, updated_at";

    public async Task<Licence?> FindByKeyHmacAsync(IReadOnlyList<byte[]> keyHmacCandidates, CancellationToken cancellationToken)
    {
        if (keyHmacCandidates.Count == 0) return null;

        const string sql = $"""
                            SELECT {LicenceColumns}
                            FROM licences
                            WHERE key_hmac = ANY(@KeyHmacs)
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { KeyHmacs = keyHmacCandidates.ToArray() },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LicenceRow>(command);
        return row?.ToDomain();
    }

    public async Task<Licence?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {LicenceColumns}
                            FROM licences
                            WHERE id = @Id
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LicenceRow>(command);
        return row?.ToDomain();
    }

    public async Task CreateAsync(Licence licence, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, expires_at, notes, ip_allowlist, created_at, updated_at)
                           VALUES (@Id, @ProductId, @UserId, @KeyHmac, @KeyHmacPepperVersion, @Status, @ExpiresAt, @Notes, @IpAllowlist::jsonb, @CreatedAt, @UpdatedAt);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                licence.Id,
                licence.ProductId,
                licence.UserId,
                licence.KeyHmac,
                licence.KeyHmacPepperVersion,
                Status = licence.Status.ToString().ToLowerInvariant(),
                licence.ExpiresAt,
                licence.Notes,
                IpAllowlist = licence.IpAllowlist is null ? null : JsonSerializer.Serialize(licence.IpAllowlist),
                licence.CreatedAt,
                licence.UpdatedAt
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<Licence>> ListAsync(
        Guid? productId,
        Guid? userId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {LicenceColumns}
                            FROM licences
                            WHERE (@ProductId::uuid IS NULL OR product_id = @ProductId::uuid)
                              AND (@UserId::uuid IS NULL OR user_id = @UserId::uuid)
                              AND (@Status::text IS NULL OR status = @Status::text)
                            ORDER BY created_at DESC
                            LIMIT @Limit OFFSET @Offset;

                            SELECT COUNT(*) FROM licences
                            WHERE (@ProductId::uuid IS NULL OR product_id = @ProductId::uuid)
                              AND (@UserId::uuid IS NULL OR user_id = @UserId::uuid)
                              AND (@Status::text IS NULL OR status = @Status::text);
                            """;

        var parameters = new
        {
            ProductId = productId,
            UserId = userId,
            Status = status?.ToString().ToLowerInvariant(),
            Limit = limit,
            Offset = offset
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<LicenceRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<Licence>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task<Licence?> UpdateStatusAsync(
        Guid licenceId,
        LicenceStatus newStatus,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
                                  SELECT {LicenceColumns}
                                  FROM licences
                                  WHERE id = @Id
                                  LIMIT 1;
                                  """;

        const string updateSql = $"""
                                  UPDATE licences
                                  SET status = @NewStatus, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
                             new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        var currentStatus = Enum.Parse<LicenceStatus>(currentRow.Status, true);
        if (currentStatus == newStatus) return currentRow.ToDomain();

        var newStatusText = newStatus.ToString().ToLowerInvariant();
        var previousStatusText = currentStatus.ToString().ToLowerInvariant();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                                 new CommandDefinition(
                                     updateSql,
                                     new { Id = licenceId, NewStatus = newStatusText },
                                     transaction,
                                     cancellationToken: cancellationToken));

            var evt = AuditEvent.Create(
                AuditEventTypes.LicenceStatusChanged,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Admin,
                changedBy,
                reason,
                new LicenceStatusChangedPayload(previousStatusText, newStatusText),
                time.GetUtcNow()
            );
            await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PagedResult<Licence>> ListForOwnerAsync(
        Guid ownerId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {LicenceColumns}
                            FROM licences
                            WHERE user_id = @OwnerId
                              AND (@Status::text IS NULL OR status = @Status::text)
                            ORDER BY created_at DESC
                            LIMIT @Limit OFFSET @Offset;

                            SELECT COUNT(*) FROM licences
                            WHERE user_id = @OwnerId
                              AND (@Status::text IS NULL OR status = @Status::text);
                            """;

        var parameters = new
        {
            OwnerId = ownerId,
            Status = status?.ToString().ToLowerInvariant(),
            Limit = limit,
            Offset = offset
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<LicenceRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<Licence>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task<PinHwidResult> PinHwidAndRecordAttemptAsync(
        Guid licenceId,
        byte[] hwidHmac,
        short hwidHmacPepperVersion,
        Guid productIdRequested,
        string sourceIp,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
                                 UPDATE licences
                                 SET hwid_hmac = @HwidHmac, hwid_hmac_pepper_version = @HwidHmacPepperVersion, updated_at = NOW()
                                 WHERE id = @Id AND hwid_hmac IS NULL
                                 RETURNING id;
                                 """;

        const string existsSql = "SELECT 1 FROM licences WHERE id = @Id LIMIT 1;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updated = await connection.QuerySingleOrDefaultAsync<Guid?>(
                              new CommandDefinition(
                                  updateSql,
                                  new { Id = licenceId, HwidHmac = hwidHmac, HwidHmacPepperVersion = hwidHmacPepperVersion },
                                  transaction,
                                  cancellationToken: cancellationToken));

            if (updated is null)
            {
                var exists = await connection.QuerySingleOrDefaultAsync<int?>(
                                 new CommandDefinition(existsSql, new { Id = licenceId }, transaction, cancellationToken: cancellationToken));
                await transaction.RollbackAsync(cancellationToken);
                return exists.HasValue ? PinHwidResult.AlreadyBound : PinHwidResult.NotFound;
            }

            var hwidBase64 = Convert.ToBase64String(hwidHmac);
            var newValueElement = JsonSerializer.SerializeToElement(
                new HwidHistoryValue(hwidBase64, sourceIp),
                AuditEventJson.Options);

            await InsertBindingChangedAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.Hwid,
                previousValue: null,
                newValue: newValueElement,
                source: BindingChangeSource.FirstUse,
                actorUserId: null,
                reason: null,
                cancellationToken);

            var verifyEvt = AuditEvent.Create(
                AuditEventTypes.LicenceVerified,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Anonymous,
                actorUserId: null,
                reason: null,
                new LicenceVerifiedPayload(
                    productIdRequested,
                    hwidBase64,
                    sourceIp,
                    Outcome: "approved",
                    DenialReason: null
                ),
                attemptedAt
            );
            await auditEvents.RecordInTxAsync(connection, transaction, verifyEvt, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return PinHwidResult.Pinned;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Licence?> ClearHwidAsync(
        Guid licenceId,
        Guid changedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
                                  SELECT {LicenceColumns}
                                  FROM licences
                                  WHERE id = @Id
                                  LIMIT 1;
                                  """;

        const string updateSql = $"""
                                  UPDATE licences
                                  SET hwid_hmac = NULL, hwid_hmac_pepper_version = NULL, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
                             new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                                 new CommandDefinition(updateSql, new { Id = licenceId }, transaction, cancellationToken: cancellationToken));

            JsonElement? previousValue = currentRow.HwidHmac is null
                                            ? null
                                            : JsonSerializer.SerializeToElement(
                                                new HwidHistoryValue(Convert.ToBase64String(currentRow.HwidHmac), null),
                                                AuditEventJson.Options);

            await InsertBindingChangedAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.Hwid,
                previousValue,
                newValue: null,
                source: BindingChangeSource.Admin,
                actorUserId: changedByUserId,
                reason: reason,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Licence?> UpdateIpAllowlistAsync(
        Guid licenceId,
        IReadOnlyList<string>? cidrs,
        Guid changedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
                                  SELECT {LicenceColumns}
                                  FROM licences
                                  WHERE id = @Id
                                  LIMIT 1;
                                  """;

        const string updateSql = $"""
                                  UPDATE licences
                                  SET ip_allowlist = @IpAllowlist::jsonb, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
                             new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        var newJson = cidrs is null ? null : JsonSerializer.Serialize(cidrs);
        var previousJson = currentRow.IpAllowlist;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                                 new CommandDefinition(
                                     updateSql,
                                     new { Id = licenceId, IpAllowlist = newJson },
                                     transaction,
                                     cancellationToken: cancellationToken));

            await InsertBindingChangedAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.IpAllowlist,
                ParseJsonElement(previousJson),
                ParseJsonElement(newJson),
                BindingChangeSource.Admin,
                changedByUserId,
                reason,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Licence?> RegenerateKeyAsync(
        Guid licenceId,
        PepperedHmac newKey,
        Guid changedBy,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
                                  SELECT {LicenceColumns}
                                  FROM licences
                                  WHERE id = @Id
                                  LIMIT 1;
                                  """;

        const string updateSql = $"""
                                  UPDATE licences
                                  SET key_hmac = @KeyHmac, key_hmac_pepper_version = @KeyHmacPepperVersion, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
                             new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                                 new CommandDefinition(
                                     updateSql,
                                     new { Id = licenceId, KeyHmac = newKey.Hmac, KeyHmacPepperVersion = newKey.PepperVersion },
                                     transaction,
                                     cancellationToken: cancellationToken));

            var evt = AuditEvent.Create(
                AuditEventTypes.LicenceKeyRegenerated,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Admin,
                changedBy,
                reason,
                new LicenceKeyRegeneratedPayload(
                    Convert.ToBase64String(currentRow.KeyHmac),
                    currentRow.KeyHmacPepperVersion,
                    Convert.ToBase64String(newKey.Hmac),
                    newKey.PepperVersion
                ),
                time.GetUtcNow()
            );
            await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IpBindResult> BindFirstUseIpAsync(
        Guid licenceId,
        string hostRoute,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
                                 UPDATE licences
                                 SET ip_allowlist = @NewValue::jsonb, updated_at = NOW()
                                 WHERE id = @Id AND ip_allowlist = '[]'::jsonb
                                 RETURNING id;
                                 """;

        const string existsSql = "SELECT 1 FROM licences WHERE id = @Id LIMIT 1;";

        var previousValueJson = JsonSerializer.Serialize(Array.Empty<string>());
        var newValueJson = JsonSerializer.Serialize(new[] { hostRoute });

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updated = await connection.QuerySingleOrDefaultAsync<Guid?>(
                              new CommandDefinition(
                                  updateSql,
                                  new { Id = licenceId, NewValue = newValueJson },
                                  transaction,
                                  cancellationToken: cancellationToken));

            if (updated is null)
            {
                var exists = await connection.QuerySingleOrDefaultAsync<int?>(
                                 new CommandDefinition(existsSql, new { Id = licenceId }, transaction, cancellationToken: cancellationToken));
                await transaction.RollbackAsync(cancellationToken);
                return exists.HasValue ? IpBindResult.AlreadyBound : IpBindResult.NotFound;
            }

            await InsertBindingChangedAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.IpAllowlist,
                ParseJsonElement(previousValueJson),
                ParseJsonElement(newValueJson),
                BindingChangeSource.FirstUse,
                null,
                null,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return IpBindResult.Bound;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task InsertBindingChangedAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid licenceId,
        LicenceBindingType bindingType,
        JsonElement? previousValue,
        JsonElement? newValue,
        BindingChangeSource source,
        Guid? actorUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var actorType = source == BindingChangeSource.Admin ? AuditActorTypes.Admin : AuditActorTypes.System;
        var evt = AuditEvent.Create(
            AuditEventTypes.LicenceBindingChanged,
            AuditSubjectTypes.Licence,
            licenceId,
            actorType,
            actorUserId,
            reason,
            new LicenceBindingChangedPayload(
                BindingTypeNames.ToString(bindingType),
                BindingChangeSourceNames.ToString(source),
                previousValue,
                newValue
            ),
            time.GetUtcNow()
        );
        await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed record HwidHistoryValue(string HwidHmacBase64, string? SourceIp);

    private sealed record LicenceRow(
        Guid Id,
        Guid ProductId,
        Guid UserId,
        byte[] KeyHmac,
        short KeyHmacPepperVersion,
        string Status,
        DateTime? ExpiresAt,
        string? Notes,
        byte[]? HwidHmac,
        short? HwidHmacPepperVersion,
        string? IpAllowlist,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        public Licence ToDomain()
        {
            IReadOnlyList<string>? allowlist = null;
            if (!string.IsNullOrEmpty(IpAllowlist)) allowlist = JsonSerializer.Deserialize<List<string>>(IpAllowlist);

            return new Licence(
                Id,
                ProductId,
                UserId,
                KeyHmac,
                KeyHmacPepperVersion,
                Enum.Parse<LicenceStatus>(Status, true),
                TimestampConversion.ToUtcOffset(ExpiresAt),
                Notes,
                HwidHmac,
                HwidHmacPepperVersion,
                allowlist,
                TimestampConversion.ToUtcOffset(CreatedAt),
                TimestampConversion.ToUtcOffset(UpdatedAt));
        }
    }
}
