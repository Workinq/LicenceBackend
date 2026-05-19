namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceSeatResponse(
    Guid Id,
    string InstanceIdHashPrefix,
    Guid? MemberUserId,
    string? HwidHmacBase64,
    string SourceIp,
    DateTimeOffset IssuedAt,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset ExpiresAt
);
