namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceStatusHistoryResponse(
    Guid           Id,
    string         PreviousStatus,
    string         NewStatus,
    Guid           ChangedBy,
    string?        ChangedByEmail,
    DateTimeOffset ChangedAt,
    string?        Reason
);
