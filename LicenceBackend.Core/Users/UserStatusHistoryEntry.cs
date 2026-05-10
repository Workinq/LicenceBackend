namespace LicenceBackend.Core.Users;

public sealed record UserStatusHistoryEntry(
    Guid           Id,
    Guid           UserId,
    UserStatus     PreviousStatus,
    UserStatus     NewStatus,
    Guid           ChangedBy,
    DateTimeOffset ChangedAt,
    string?        Reason
);
