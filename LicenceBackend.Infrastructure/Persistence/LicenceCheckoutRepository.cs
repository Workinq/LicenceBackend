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

    public Task<LicenceCheckout?> HeartbeatAsync(Guid checkoutId, TimeSpan leaseDuration, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<bool> CloseAsync(Guid checkoutId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<bool> ForceRevokeAsync(Guid checkoutId, LicenceCheckoutCloseReason reason, Guid actorUserId, string? actorReason, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<ReclaimExpiredResult> ReclaimExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<LicenceCheckout>> ListLiveForLicenceAsync(Guid licenceId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<PagedResult<LicenceCheckoutHistoryEntry>> ListHistoryForLicenceAsync(Guid licenceId, int limit, int offset, CancellationToken cancellationToken)
        => throw new NotImplementedException();

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
