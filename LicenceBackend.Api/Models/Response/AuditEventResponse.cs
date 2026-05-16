using System.Text.Json;

namespace LicenceBackend.Api.Models.Response;

public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string EventType,
    string SubjectType,
    Guid SubjectId,
    string ActorType,
    Guid? ActorUserId,
    string? ActorUserEmail,
    string? Reason,
    JsonElement Payload
);
