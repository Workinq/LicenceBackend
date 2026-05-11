using System.Data;
using System.Text.Json;
using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceRepository(NpgsqlDataSource dataSource) : ILicenceRepository
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

        const string insertHistorySql = """
                                        INSERT INTO licence_status_history (id, licence_id, previous_status, new_status, changed_by, reason)
                                        VALUES (@Id, @LicenceId, @PreviousStatus, @NewStatus, @ChangedBy, @Reason);
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

            await connection.ExecuteAsync(new CommandDefinition(
                                              insertHistorySql,
                                              new
                                              {
                                                  Id = Guid.NewGuid(),
                                                  LicenceId = licenceId,
                                                  PreviousStatus = previousStatusText,
                                                  NewStatus = newStatusText,
                                                  ChangedBy = changedBy,
                                                  Reason = reason
                                              },
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
        string sourceIp,
        LicenceVerificationAttempt approvedAttempt,
        CancellationToken cancellationToken)
    {
        const string updateSql = """
                                 UPDATE licences
                                 SET hwid_hmac = @HwidHmac, hwid_hmac_pepper_version = @HwidHmacPepperVersion, updated_at = NOW()
                                 WHERE id = @Id AND hwid_hmac IS NULL
                                 RETURNING id;
                                 """;

        const string existsSql = "SELECT 1 FROM licences WHERE id = @Id LIMIT 1;";

        const string insertAttemptSql = """
                                        INSERT INTO licence_verification_attempts (
                                            id, licence_id, product_id_requested, hwid_hmac,
                                            source_ip, outcome, denial_reason, attempted_at
                                        ) VALUES (
                                            @Id, @LicenceId, @ProductIdRequested, @HwidHmac,
                                            @SourceIp::inet, @Outcome, @DenialReason, @AttemptedAt
                                        );
                                        """;

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

            var newValueJson = JsonSerializer.Serialize(new HwidHistoryValue(
                                                            Convert.ToBase64String(hwidHmac),
                                                            sourceIp));

            await InsertBindingHistoryAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.Hwid,
                null,
                newValueJson,
                BindingChangeSource.FirstUse,
                null,
                null,
                cancellationToken);

            await connection.ExecuteAsync(new CommandDefinition(
                                              insertAttemptSql,
                                              new
                                              {
                                                  approvedAttempt.Id,
                                                  approvedAttempt.LicenceId,
                                                  approvedAttempt.ProductIdRequested,
                                                  approvedAttempt.HwidHmac,
                                                  approvedAttempt.SourceIp,
                                                  Outcome = LicenceVerificationAttemptRepository.OutcomeToString(approvedAttempt.Outcome),
                                                  DenialReason = LicenceVerificationAttemptRepository.DenialReasonToString(approvedAttempt.DenialReason),
                                                  approvedAttempt.AttemptedAt
                                              },
                                              transaction,
                                              cancellationToken: cancellationToken));

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

            var previousValueJson = currentRow.HwidHmac is null
                                        ? null
                                        : JsonSerializer.Serialize(new HwidHistoryValue(
                                                                       Convert.ToBase64String(currentRow.HwidHmac),
                                                                       null));

            await InsertBindingHistoryAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.Hwid,
                previousValueJson,
                null,
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

            await InsertBindingHistoryAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.IpAllowlist,
                previousJson,
                newJson,
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

            await InsertBindingHistoryAsync(
                connection,
                transaction,
                licenceId,
                LicenceBindingType.IpAllowlist,
                previousValueJson,
                newValueJson,
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

    private static async Task InsertBindingHistoryAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid licenceId,
        LicenceBindingType bindingType,
        string? previousValueJson,
        string? newValueJson,
        BindingChangeSource source,
        Guid? changedByUserId,
        string? reason,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO licence_binding_history (
                               id, licence_id, binding_type, previous_value, new_value,
                               change_source, changed_by_user_id, reason
                           ) VALUES (
                               @Id, @LicenceId, @BindingType, @PreviousValue::jsonb, @NewValue::jsonb,
                               @ChangeSource, @ChangedByUserId, @Reason
                           );
                           """;

        await connection.ExecuteAsync(new CommandDefinition(
                                          sql,
                                          new
                                          {
                                              Id = Guid.NewGuid(),
                                              LicenceId = licenceId,
                                              BindingType = BindingTypeToString(bindingType),
                                              PreviousValue = previousValueJson,
                                              NewValue = newValueJson,
                                              ChangeSource = ChangeSourceToString(source),
                                              ChangedByUserId = changedByUserId,
                                              Reason = reason
                                          },
                                          transaction,
                                          cancellationToken: cancellationToken));
    }

    private static string BindingTypeToString(LicenceBindingType type)
    {
        return type switch
        {
            LicenceBindingType.Hwid => "hwid",
            LicenceBindingType.IpAllowlist => "ip_allowlist",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static string ChangeSourceToString(BindingChangeSource source)
    {
        return source switch
        {
            BindingChangeSource.Admin => "admin",
            BindingChangeSource.FirstUse => "first_use",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };
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
