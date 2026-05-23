using System.Text.Json;

namespace LicenceBackend.Core.Auditing;

public sealed record AuditEventDraft(
    string EventType,
    string SubjectType,
    Guid SubjectId,
    string ActorType,
    Guid? ActorUserId,
    string? Reason,
    object Payload,
    DateTimeOffset OccurredAt
);

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
    public static AuditEvent Create(AuditEventDraft draft)
    {
        return new AuditEvent(
            Guid.NewGuid(),
            draft.OccurredAt,
            draft.EventType,
            draft.SubjectType,
            draft.SubjectId,
            draft.ActorType,
            draft.ActorUserId,
            draft.Reason,
            JsonSerializer.Serialize(draft.Payload, AuditEventJson.Options)
        );
    }

    public T DeserializePayload<T>()
    {
        var value = JsonSerializer.Deserialize<T>(PayloadJson, AuditEventJson.Options);
        if (value is null) throw new InvalidOperationException($"Audit event {Id} payload could not be deserialized as {typeof(T).Name}.");
        return value;
    }
}
