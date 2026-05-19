using Dapper;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Licences;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class LicenceCheckoutRepository(
    NpgsqlDataSource dataSource,
    IAuditEventRepository auditEvents,
    TimeProvider time
) : ILicenceCheckoutRepository
{
    private const string CheckoutColumns =
        "id, licence_id, instance_id_hash, member_user_id, hwid_hmac, hwid_hmac_pepper_version, source_ip::text AS source_ip, issued_at, last_heartbeat_at, expires_at";

    public async Task<OpenCheckoutOutcome> OpenAsync(
        Guid licenceId,
        byte[] instanceIdHash,
        Guid? memberUserId,
        byte[]? hwidHmac,
        short? hwidHmacPepperVersion,
        string sourceIp,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        const string licenceLookupSql = "SELECT max_seats FROM licences WHERE id = @LicenceId LIMIT 1;";
        const string reclaimSql = """
                                   WITH expired AS (
                                       DELETE FROM licence_checkouts
                                       WHERE licence_id = @LicenceId AND expires_at <= @Now
                                       RETURNING id, licence_id, instance_id_hash, member_user_id, hwid_hmac, source_ip::text AS source_ip, issued_at
                                   )
                                   INSERT INTO licence_checkout_history
                                       (id, licence_id, checkout_id, instance_id_hash, member_user_id, hwid_hmac, source_ip, issued_at, closed_at, close_reason)
                                   SELECT gen_random_uuid(), licence_id, id, instance_id_hash, member_user_id, hwid_hmac, source_ip::inet, issued_at, @Now, 'expired'
                                   FROM expired;
                                   """;
        const string idempotentLookupSql = $"""
                                             SELECT {CheckoutColumns}
                                             FROM licence_checkouts
                                             WHERE licence_id = @LicenceId AND instance_id_hash = @Hash
                                             LIMIT 1;
                                             """;
        const string countSql = "SELECT COUNT(*) FROM licence_checkouts WHERE licence_id = @LicenceId;";
        const string oldestExpirySql = "SELECT MIN(expires_at) FROM licence_checkouts WHERE licence_id = @LicenceId;";
        const string insertSql = """
                                  INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, member_user_id, hwid_hmac, hwid_hmac_pepper_version, source_ip, issued_at, last_heartbeat_at, expires_at)
                                  VALUES (@Id, @LicenceId, @Hash, @MemberUserId, @HwidHmac, @HwidHmacPepperVersion, @SourceIp::inet, @Now, @Now, @ExpiresAt);
                                  """;
        const string advisoryLockSql = "SELECT pg_advisory_xact_lock(hashtextextended(@Key, 0));";

        var now = time.GetUtcNow();
        var expiresAt = now.Add(leaseDuration);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var maxSeats = await connection.QuerySingleOrDefaultAsync<int?>(
                               new CommandDefinition(licenceLookupSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
            if (maxSeats is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new OpenCheckoutOutcome.LicenceNotFound();
            }

            await connection.ExecuteAsync(new CommandDefinition(
                advisoryLockSql,
                new { Key = licenceId.ToString() },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(reclaimSql, new { LicenceId = licenceId, Now = now }, transaction, cancellationToken: cancellationToken));

            var existing = await connection.QuerySingleOrDefaultAsync<CheckoutRow>(
                               new CommandDefinition(idempotentLookupSql, new { LicenceId = licenceId, Hash = instanceIdHash }, transaction, cancellationToken: cancellationToken));
            if (existing is not null)
            {
                var seatsAfterExisting = await connection.QuerySingleAsync<int>(
                                              new CommandDefinition(countSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                return new OpenCheckoutOutcome.Opened(new OpenCheckoutResult(existing.ToDomain(), seatsAfterExisting, maxSeats.Value, IsIdempotentReplay: true));
            }

            var activeSeats = await connection.QuerySingleAsync<int>(
                                  new CommandDefinition(countSql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
            if (activeSeats >= maxSeats.Value)
            {
                var oldestExpiry = await connection.QuerySingleAsync<DateTime>(
                                       new CommandDefinition(oldestExpirySql, new { LicenceId = licenceId }, transaction, cancellationToken: cancellationToken));
                var oldestExpiryOffset = TimestampConversion.ToUtcOffset(oldestExpiry);

                var denyEvt = AuditEvent.Create(
                    AuditEventTypes.LicenceCheckoutDeniedNoSeats,
                    AuditSubjectTypes.Licence,
                    licenceId,
                    AuditActorTypes.Anonymous,
                    actorUserId: null,
                    reason: null,
                    new LicenceCheckoutDeniedNoSeatsPayload(
                        InstanceHashPrefix(instanceIdHash),
                        memberUserId,
                        hwidHmac is null ? null : Convert.ToBase64String(hwidHmac),
                        sourceIp,
                        activeSeats,
                        maxSeats.Value,
                        oldestExpiryOffset
                    ),
                    now
                );
                await auditEvents.RecordInTxAsync(connection, transaction, denyEvt, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new OpenCheckoutOutcome.DeniedNoSeats(new DeniedNoSeatsResult(activeSeats, maxSeats.Value, oldestExpiryOffset));
            }

            var newId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new
                {
                    Id = newId,
                    LicenceId = licenceId,
                    Hash = instanceIdHash,
                    MemberUserId = memberUserId,
                    HwidHmac = hwidHmac,
                    HwidHmacPepperVersion = hwidHmacPepperVersion,
                    SourceIp = sourceIp,
                    Now = now,
                    ExpiresAt = expiresAt
                },
                transaction,
                cancellationToken: cancellationToken));

            var seatsAfter = activeSeats + 1;
            var openedEvt = AuditEvent.Create(
                AuditEventTypes.LicenceCheckoutOpened,
                AuditSubjectTypes.Licence,
                licenceId,
                AuditActorTypes.Anonymous,
                actorUserId: null,
                reason: null,
                new LicenceCheckoutOpenedPayload(
                    newId,
                    InstanceHashPrefix(instanceIdHash),
                    memberUserId,
                    hwidHmac is null ? null : Convert.ToBase64String(hwidHmac),
                    sourceIp,
                    seatsAfter,
                    maxSeats.Value
                ),
                now
            );
            await auditEvents.RecordInTxAsync(connection, transaction, openedEvt, cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var checkout = new LicenceCheckout(
                newId, licenceId, instanceIdHash, memberUserId, hwidHmac, hwidHmacPepperVersion,
                sourceIp, now, now, expiresAt);
            return new OpenCheckoutOutcome.Opened(new OpenCheckoutResult(checkout, seatsAfter, maxSeats.Value, IsIdempotentReplay: false));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<LicenceCheckout?> HeartbeatAsync(
        Guid checkoutId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
                            UPDATE licence_checkouts
                            SET last_heartbeat_at = @Now,
                                expires_at = @NewExpiry
                            WHERE id = @Id AND expires_at > @Now
                            RETURNING {CheckoutColumns};
                            """;

        var now = time.GetUtcNow();
        var newExpiry = now.Add(leaseDuration);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CheckoutRow>(
                      new CommandDefinition(sql, new { Id = checkoutId, Now = now, NewExpiry = newExpiry }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task<bool> CloseAsync(Guid checkoutId, CancellationToken cancellationToken)
    {
        return await ArchiveAndDeleteAsync(
            checkoutId,
            LicenceCheckoutCloseReason.Checkin,
            actor: null,
            actorReason: null,
            cancellationToken);
    }

    public async Task<bool> ForceRevokeAsync(
        Guid checkoutId,
        LicenceCheckoutCloseReason reason,
        Guid actorUserId,
        string? actorReason,
        CancellationToken cancellationToken)
    {
        if (reason != LicenceCheckoutCloseReason.AdminRevoked && reason != LicenceCheckoutCloseReason.OwnerRevoked)
            throw new ArgumentException($"ForceRevoke requires AdminRevoked or OwnerRevoked, got {reason}.", nameof(reason));

        return await ArchiveAndDeleteAsync(
            checkoutId,
            reason,
            actor: actorUserId,
            actorReason,
            cancellationToken);
    }

    private async Task<bool> ArchiveAndDeleteAsync(
        Guid checkoutId,
        LicenceCheckoutCloseReason reason,
        Guid? actor,
        string? actorReason,
        CancellationToken cancellationToken)
    {
        const string deleteSql = $"""
                                  DELETE FROM licence_checkouts WHERE id = @Id
                                  RETURNING {CheckoutColumns};
                                  """;
        const string archiveSql = """
                                   INSERT INTO licence_checkout_history
                                       (id, licence_id, checkout_id, instance_id_hash, member_user_id, hwid_hmac, source_ip, issued_at, closed_at, close_reason)
                                   VALUES (@Id, @LicenceId, @CheckoutId, @InstanceIdHash, @MemberUserId, @HwidHmac, @SourceIp::inet, @IssuedAt, @ClosedAt, @CloseReason);
                                   """;
        const string countSql = "SELECT COUNT(*) FROM licence_checkouts WHERE licence_id = @LicenceId;";
        const string maxSeatsSql = "SELECT max_seats FROM licences WHERE id = @LicenceId;";

        var now = time.GetUtcNow();
        var reasonText = LicenceCheckoutCloseReasonNames.ToString(reason);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync<CheckoutRow>(
                          new CommandDefinition(deleteSql, new { Id = checkoutId }, transaction, cancellationToken: cancellationToken));
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                archiveSql,
                new
                {
                    Id = Guid.NewGuid(),
                    row.LicenceId,
                    CheckoutId = row.Id,
                    row.InstanceIdHash,
                    row.MemberUserId,
                    row.HwidHmac,
                    row.SourceIp,
                    IssuedAt = TimestampConversion.ToUtcOffset(row.IssuedAt),
                    ClosedAt = now,
                    CloseReason = reasonText
                },
                transaction,
                cancellationToken: cancellationToken));

            if (reason == LicenceCheckoutCloseReason.AdminRevoked || reason == LicenceCheckoutCloseReason.OwnerRevoked)
            {
                var seatsAfter = await connection.QuerySingleAsync<int>(
                                     new CommandDefinition(countSql, new { row.LicenceId }, transaction, cancellationToken: cancellationToken));
                var maxSeats = await connection.QuerySingleAsync<int>(
                                   new CommandDefinition(maxSeatsSql, new { row.LicenceId }, transaction, cancellationToken: cancellationToken));

                var actorType = reason == LicenceCheckoutCloseReason.AdminRevoked
                    ? AuditActorTypes.Admin
                    : AuditActorTypes.User;
                var evt = AuditEvent.Create(
                    AuditEventTypes.LicenceCheckoutClosed,
                    AuditSubjectTypes.Licence,
                    row.LicenceId,
                    actorType,
                    actor,
                    actorReason,
                    new LicenceCheckoutClosedPayload(
                        row.Id,
                        InstanceHashPrefix(row.InstanceIdHash),
                        row.MemberUserId,
                        row.HwidHmac is null ? null : Convert.ToBase64String(row.HwidHmac),
                        row.SourceIp,
                        reasonText,
                        seatsAfter,
                        maxSeats
                    ),
                    now
                );
                await auditEvents.RecordInTxAsync(connection, transaction, evt, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ReclaimExpiredResult> ReclaimExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
                            WITH expired AS (
                                DELETE FROM licence_checkouts
                                WHERE expires_at <= @Now
                                RETURNING id, licence_id, instance_id_hash, member_user_id, hwid_hmac, source_ip::text AS source_ip, issued_at
                            ),
                            archived AS (
                                INSERT INTO licence_checkout_history
                                    (id, licence_id, checkout_id, instance_id_hash, member_user_id, hwid_hmac, source_ip, issued_at, closed_at, close_reason)
                                SELECT gen_random_uuid(), licence_id, id, instance_id_hash, member_user_id, hwid_hmac, source_ip::inet, issued_at, @Now, 'expired'
                                FROM expired
                                RETURNING licence_id
                            )
                            SELECT COUNT(*) AS reclaimed_count, COUNT(DISTINCT licence_id) AS licences_affected FROM archived;
                            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var (reclaimedCount, licencesAffected) = await connection.QuerySingleAsync<(int reclaimed_count, int licences_affected)>(
            new CommandDefinition(sql, new { Now = now }, cancellationToken: cancellationToken));
        return new ReclaimExpiredResult(reclaimedCount, licencesAffected);
    }

    public async Task<IReadOnlyList<LicenceCheckout>> ListLiveForLicenceAsync(
        Guid licenceId,
        CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {CheckoutColumns}
                            FROM licence_checkouts
                            WHERE licence_id = @LicenceId AND expires_at > @Now
                            ORDER BY issued_at DESC;
                            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CheckoutRow>(
                       new CommandDefinition(sql, new { LicenceId = licenceId, Now = time.GetUtcNow() }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<PagedResult<LicenceCheckoutHistoryEntry>> ListHistoryForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
                            SELECT id, licence_id, checkout_id, instance_id_hash, member_user_id, hwid_hmac, source_ip::text AS source_ip, issued_at, closed_at, close_reason
                            FROM licence_checkout_history
                            WHERE licence_id = @LicenceId
                            ORDER BY closed_at DESC
                            LIMIT @Limit OFFSET @Offset;

                            SELECT COUNT(*) FROM licence_checkout_history WHERE licence_id = @LicenceId;
                            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId, Limit = limit, Offset = offset }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<HistoryRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<LicenceCheckoutHistoryEntry>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private static string InstanceHashPrefix(byte[] instanceIdHash) =>
        Convert.ToHexString(instanceIdHash.AsSpan(0, Math.Min(8, instanceIdHash.Length))).ToLowerInvariant();

    private sealed record CheckoutRow(
        Guid Id,
        Guid LicenceId,
        byte[] InstanceIdHash,
        Guid? MemberUserId,
        byte[]? HwidHmac,
        short? HwidHmacPepperVersion,
        string SourceIp,
        DateTime IssuedAt,
        DateTime LastHeartbeatAt,
        DateTime ExpiresAt
    )
    {
        public LicenceCheckout ToDomain() => new(
            Id, LicenceId, InstanceIdHash, MemberUserId, HwidHmac, HwidHmacPepperVersion, SourceIp,
            TimestampConversion.ToUtcOffset(IssuedAt),
            TimestampConversion.ToUtcOffset(LastHeartbeatAt),
            TimestampConversion.ToUtcOffset(ExpiresAt)
        );
    }

    private sealed record HistoryRow(
        Guid Id,
        Guid LicenceId,
        Guid CheckoutId,
        byte[] InstanceIdHash,
        Guid? MemberUserId,
        byte[]? HwidHmac,
        string SourceIp,
        DateTime IssuedAt,
        DateTime ClosedAt,
        string CloseReason
    )
    {
        public LicenceCheckoutHistoryEntry ToDomain() => new(
            Id, LicenceId, CheckoutId, InstanceIdHash, MemberUserId, HwidHmac, SourceIp,
            TimestampConversion.ToUtcOffset(IssuedAt),
            TimestampConversion.ToUtcOffset(ClosedAt),
            LicenceCheckoutCloseReasonNames.Parse(CloseReason)
        );
    }
}
