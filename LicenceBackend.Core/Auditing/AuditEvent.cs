using System.Text.Json;

namespace LicenceBackend.Core.Auditing;

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string SubjectType,
    Guid SubjectId,
    string ActorType,
    Guid? ActorUserId,
    string? Reason,
    string PayloadJson
)
{
    public static AuditEvent Create(
        string eventType,
        string subjectType,
        Guid subjectId,
        string actorType,
        Guid? actorUserId,
        string? reason,
        object payload,
        DateTimeOffset occurredAt
    )
    {
        return new AuditEvent(
            Guid.NewGuid(),
            occurredAt,
            eventType,
            subjectType,
            subjectId,
            actorType,
            actorUserId,
            reason,
            JsonSerializer.Serialize(payload, AuditEventJson.Options)
        );
    }

    public T DeserializePayload<T>()
    {
        var value = JsonSerializer.Deserialize<T>(PayloadJson, AuditEventJson.Options);
        if (value is null) throw new InvalidOperationException($"Audit event {Id} payload could not be deserialized as {typeof(T).Name}.");
        return value;
    }
}
