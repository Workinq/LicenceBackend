using System.Data;
using Dapper;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Common;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class AuditEventRepository(NpgsqlDataSource dataSource) : IAuditEventRepository
{
    private const string InsertSql = """
                                     INSERT INTO audit_events (
                                         id, occurred_at, event_type, subject_type, subject_id,
                                         actor_type, actor_user_id, reason, payload
                                     ) VALUES (
                                         @Id, @OccurredAt, @EventType, @SubjectType, @SubjectId,
                                         @ActorType, @ActorUserId, @Reason, @PayloadJson::jsonb
                                     );
                                     """;

    public async Task RecordAsync(AuditEvent evt, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(InsertSql, ToParameters(evt), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task RecordInTxAsync(IDbConnection connection, IDbTransaction transaction, AuditEvent evt, CancellationToken cancellationToken)
    {
        var command = new CommandDefinition(InsertSql, ToParameters(evt), transaction, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<AuditEvent>> QueryAsync(
        string? subjectType,
        Guid? subjectId,
        IReadOnlyList<string>? eventTypes,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, occurred_at, event_type, subject_type, subject_id,
                                  actor_type, actor_user_id, reason, payload::text AS payload_json
                           FROM audit_events
                           WHERE (@SubjectType::text IS NULL OR subject_type = @SubjectType::text)
                             AND (@SubjectId::uuid    IS NULL OR subject_id   = @SubjectId::uuid)
                             AND (@EventTypes::text[] IS NULL OR event_type = ANY(@EventTypes::text[]))
                           ORDER BY occurred_at DESC, id DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM audit_events
                           WHERE (@SubjectType::text IS NULL OR subject_type = @SubjectType::text)
                             AND (@SubjectId::uuid    IS NULL OR subject_id   = @SubjectId::uuid)
                             AND (@EventTypes::text[] IS NULL OR event_type = ANY(@EventTypes::text[]));
                           """;

        var eventTypeArray = eventTypes is { Count: > 0 } ? eventTypes.ToArray() : null;
        var parameters = new
        {
            SubjectType = subjectType,
            SubjectId = subjectId,
            EventTypes = eventTypeArray,
            Limit = limit,
            Offset = offset
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<Row>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<AuditEvent>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task<PagedResult<AuditEvent>> QueryVerifiesAsync(
        Guid? licenceId,
        string? outcome,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, occurred_at, event_type, subject_type, subject_id,
                                  actor_type, actor_user_id, reason, payload::text AS payload_json
                           FROM audit_events
                           WHERE event_type = 'licence.verified'
                             AND (@LicenceId::uuid IS NULL OR subject_id = @LicenceId::uuid)
                             AND (@Outcome::text  IS NULL OR (payload->>'outcome') = @Outcome::text)
                           ORDER BY occurred_at DESC, id DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM audit_events
                           WHERE event_type = 'licence.verified'
                             AND (@LicenceId::uuid IS NULL OR subject_id = @LicenceId::uuid)
                             AND (@Outcome::text  IS NULL OR (payload->>'outcome') = @Outcome::text);
                           """;

        var parameters = new
        {
            LicenceId = licenceId,
            Outcome = outcome,
            Limit = limit,
            Offset = offset
        };

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<Row>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<AuditEvent>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private static object ToParameters(AuditEvent evt)
    {
        return new
        {
            evt.Id,
            OccurredAt = evt.OccurredAt.UtcDateTime,
            evt.EventType,
            evt.SubjectType,
            evt.SubjectId,
            evt.ActorType,
            evt.ActorUserId,
            evt.Reason,
            evt.PayloadJson
        };
    }

    private sealed record Row(
        Guid Id,
        DateTime OccurredAt,
        string EventType,
        string SubjectType,
        Guid SubjectId,
        string ActorType,
        Guid? ActorUserId,
        string? Reason,
        string PayloadJson
    )
    {
        public AuditEvent ToDomain()
        {
            return new AuditEvent(
                Id,
                TimestampConversion.ToUtcOffset(OccurredAt),
                EventType,
                SubjectType,
                SubjectId,
                ActorType,
                ActorUserId,
                Reason,
                PayloadJson
            );
        }
    }
}
