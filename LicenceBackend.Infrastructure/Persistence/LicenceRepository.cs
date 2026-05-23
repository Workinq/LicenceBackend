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
        "id, product_id, user_id, status, expires_at, notes, hwid_hmac, hwid_hmac_pepper_version, ip_allowlist, label, max_seats, created_at, updated_at";

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
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(InsertLicenceSql, BuildInsertParameters(licence), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task CreateInTxAsync(IDbConnection connection, IDbTransaction transaction, Licence licence, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(InsertLicenceSql, BuildInsertParameters(licence), transaction, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<Licence?> UpdateLabelAsync(Guid licenceId, Guid ownerId, string? label, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            UPDATE licences
                            SET label = @Label, updated_at = NOW()
                            WHERE id = @Id AND user_id = @OwnerId
                            RETURNING {LicenceColumns};
                            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = licenceId, OwnerId = ownerId, Label = label },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LicenceRow>(command);
        return row?.ToDomain();
    }

    private const string InsertLicenceSql = """
                                             INSERT INTO licences (id, product_id, user_id, status, expires_at, notes, ip_allowlist, label, max_seats, created_at, updated_at)
                                             VALUES (@Id, @ProductId, @UserId, @Status, @ExpiresAt, @Notes, @IpAllowlist::jsonb, @Label, @MaxSeats, @CreatedAt, @UpdatedAt);
                                             """;

    private static object BuildInsertParameters(Licence licence) => new
    {
        licence.Id,
        licence.ProductId,
        licence.UserId,
        Status = licence.Status.ToString().ToLowerInvariant(),
        licence.ExpiresAt,
        licence.Notes,
        IpAllowlist = licence.IpAllowlist is null ? null : JsonSerializer.Serialize(licence.IpAllowlist),
        licence.Label,
        licence.MaxSeats,
        licence.CreatedAt,
        licence.UpdatedAt
    };

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

        return await ExecuteListAsync<LicenceRow, Licence>(sql, parameters, r => r.ToDomain(), cancellationToken);
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

            var evt = AuditEvent.Create(new AuditEventDraft(
                AuditEventTypes.LicenceStatusChanged,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Admin,
                changedBy,
                reason,
                new LicenceStatusChangedPayload(previousStatusText, newStatusText),
                time.GetUtcNow()
            ));
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

    public async Task<Licence?> UpdateMaxSeatsAsync(
        Guid licenceId,
        int newMaxSeats,
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
                                  SET max_seats = @MaxSeats, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
                             new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        var previousMaxSeats = currentRow.MaxSeats;
        if (previousMaxSeats == newMaxSeats) return currentRow.ToDomain();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                                 new CommandDefinition(
                                     updateSql,
                                     new { Id = licenceId, MaxSeats = newMaxSeats },
                                     transaction,
                                     cancellationToken: cancellationToken));

            var evt = AuditEvent.Create(new AuditEventDraft(
                AuditEventTypes.LicenceMaxSeatsUpdated,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Admin,
                changedBy,
                reason,
                new LicenceMaxSeatsUpdatedPayload(previousMaxSeats, newMaxSeats),
                time.GetUtcNow()
            ));
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

    public async Task<PagedResult<UserLicence>> ListForUserAsync(
        Guid userId,
        LicenceStatus? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {LicenceColumns},
                                   CASE WHEN user_id = @UserId THEN 'owner' ELSE 'member' END AS relationship
                            FROM licences
                            WHERE (user_id = @UserId
                                OR EXISTS (SELECT 1 FROM licence_members m WHERE m.licence_id = licences.id AND m.user_id = @UserId))
                              AND (@Status::text IS NULL OR status = @Status::text)
                            ORDER BY created_at DESC
                            LIMIT @Limit OFFSET @Offset;

                            SELECT COUNT(*) FROM licences
                            WHERE (user_id = @UserId
                                OR EXISTS (SELECT 1 FROM licence_members m WHERE m.licence_id = licences.id AND m.user_id = @UserId))
                              AND (@Status::text IS NULL OR status = @Status::text);
                            """;

        var parameters = new
        {
            UserId = userId,
            Status = status?.ToString().ToLowerInvariant(),
            Limit = limit,
            Offset = offset
        };

        return await ExecuteListAsync<UserLicenceRow, UserLicence>(
            sql,
            parameters,
            r => new UserLicence(r.ToLicenceRow().ToDomain(), r.Relationship),
            cancellationToken);
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

        return await ExecuteListAsync<LicenceRow, Licence>(sql, parameters, r => r.ToDomain(), cancellationToken);
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
                new BindingChange(
                    licenceId,
                    LicenceBindingType.Hwid,
                    PreviousValue: null,
                    NewValue: newValueElement,
                    Source: BindingChangeSource.FirstUse,
                    ActorUserId: null,
                    Reason: null),
                cancellationToken);

            var verifyEvt = AuditEvent.Create(new AuditEventDraft(
                AuditEventTypes.LicenceVerified,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Anonymous,
                ActorUserId: null,
                Reason: null,
                new LicenceVerifiedPayload(
                    productIdRequested,
                    hwidBase64,
                    sourceIp,
                    Outcome: "approved",
                    DenialReason: null
                ),
                attemptedAt
            ));
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
        const string updateSql = $"""
                                  UPDATE licences
                                  SET hwid_hmac = NULL, hwid_hmac_pepper_version = NULL, updated_at = NOW()
                                  WHERE id = @Id
                                  RETURNING {LicenceColumns};
                                  """;

        return await UpdateLicenceWithAuditAsync(
            licenceId,
            updateSql,
            new { Id = licenceId },
            async (conn, tx, currentRow) =>
            {
                JsonElement? previousValue = currentRow.HwidHmac is null
                    ? null
                    : JsonSerializer.SerializeToElement(
                        new HwidHistoryValue(Convert.ToBase64String(currentRow.HwidHmac), null),
                        AuditEventJson.Options);

                await InsertBindingChangedAsync(
                    conn,
                    tx,
                    new BindingChange(
                        licenceId,
                        LicenceBindingType.Hwid,
                        previousValue,
                        NewValue: null,
                        Source: BindingChangeSource.Admin,
                        ActorUserId: changedByUserId,
                        Reason: reason),
                    cancellationToken);
            },
            cancellationToken);
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
                new BindingChange(
                    licenceId,
                    LicenceBindingType.IpAllowlist,
                    ParseJsonElement(previousJson),
                    ParseJsonElement(newJson),
                    BindingChangeSource.Admin,
                    changedByUserId,
                    reason),
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
                new BindingChange(
                    licenceId,
                    LicenceBindingType.IpAllowlist,
                    ParseJsonElement(previousValueJson),
                    ParseJsonElement(newValueJson),
                    BindingChangeSource.FirstUse,
                    null,
                    null),
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

    private async Task<PagedResult<T>> ExecuteListAsync<TRow, T>(
        string sql,
        object parameters,
        Func<TRow, T> mapper,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<TRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return new PagedResult<T>(rows.Select(mapper).ToList(), total);
    }

    private async Task<Licence?> UpdateLicenceWithAuditAsync(
        Guid licenceId,
        string updateSql,
        object updateParams,
        Func<IDbConnection, IDbTransaction, LicenceRow, Task> writeAuditAsync,
        CancellationToken cancellationToken)
    {
        const string selectSql = $"""
                                  SELECT {LicenceColumns}
                                  FROM licences
                                  WHERE id = @Id
                                  LIMIT 1;
                                  """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var currentRow = await connection.QuerySingleOrDefaultAsync<LicenceRow>(
            new CommandDefinition(selectSql, new { Id = licenceId }, cancellationToken: cancellationToken));
        if (currentRow is null) return null;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var updatedRow = await connection.QuerySingleAsync<LicenceRow>(
                new CommandDefinition(updateSql, updateParams, transaction, cancellationToken: cancellationToken));
            await writeAuditAsync(connection, transaction, currentRow);
            await transaction.CommitAsync(cancellationToken);
            return updatedRow.ToDomain();
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
        BindingChange change,
        CancellationToken cancellationToken)
    {
        var actorType = change.Source == BindingChangeSource.Admin ? AuditActorTypes.Admin : AuditActorTypes.System;
        var evt = AuditEvent.Create(new AuditEventDraft(
            AuditEventTypes.LicenceBindingChanged,
            AuditSubjectTypes.Licence,
            change.LicenceId,
            actorType,
            change.ActorUserId,
            change.Reason,
            new LicenceBindingChangedPayload(
                BindingTypeNames.ToString(change.BindingType),
                BindingChangeSourceNames.ToString(change.Source),
                change.PreviousValue,
                change.NewValue
            ),
            time.GetUtcNow()
        ));
        await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);
    }

    private sealed record BindingChange(
        Guid LicenceId,
        LicenceBindingType BindingType,
        JsonElement? PreviousValue,
        JsonElement? NewValue,
        BindingChangeSource Source,
        Guid? ActorUserId,
        string? Reason
    );

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed record HwidHistoryValue(string HwidHmacBase64, string? SourceIp);

    private sealed record UserLicenceRow(
        Guid Id,
        Guid ProductId,
        Guid UserId,
        string Status,
        DateTime? ExpiresAt,
        string? Notes,
        byte[]? HwidHmac,
        short? HwidHmacPepperVersion,
        string? IpAllowlist,
        string? Label,
        int MaxSeats,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        string Relationship
    )
    {
        public LicenceRow ToLicenceRow() => new(
            Id, ProductId, UserId, Status,
            ExpiresAt, Notes, HwidHmac, HwidHmacPepperVersion, IpAllowlist,
            Label, MaxSeats, CreatedAt, UpdatedAt
        );
    }

    private sealed record LicenceRow(
        Guid Id,
        Guid ProductId,
        Guid UserId,
        string Status,
        DateTime? ExpiresAt,
        string? Notes,
        byte[]? HwidHmac,
        short? HwidHmacPepperVersion,
        string? IpAllowlist,
        string? Label,
        int MaxSeats,
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
                Enum.Parse<LicenceStatus>(Status, true),
                TimestampConversion.ToUtcOffset(ExpiresAt),
                Notes,
                HwidHmac,
                HwidHmacPepperVersion,
                allowlist,
                Label,
                MaxSeats,
                TimestampConversion.ToUtcOffset(CreatedAt),
                TimestampConversion.ToUtcOffset(UpdatedAt));
        }
    }
}
