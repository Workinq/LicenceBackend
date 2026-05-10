namespace LicenceBackend.Core.Licences;

public sealed record LicenceStatusHistoryEntry(
    Guid Id,
    Guid LicenceId,
    LicenceStatus PreviousStatus,
    LicenceStatus NewStatus,
    Guid ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason
);
