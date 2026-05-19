namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceSeatHistoryEntryResponse(
    Guid Id,
    Guid CheckoutId,
    string InstanceIdHashPrefix,
    Guid? MemberUserId,
    string? HwidHmacBase64,
    string SourceIp,
    DateTimeOffset IssuedAt,
    DateTimeOffset ClosedAt,
    string CloseReason
);
