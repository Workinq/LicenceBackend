namespace LicenceBackend.Core.Users;

public sealed record User(
    Guid Id,
    string Email,
    string EmailLower,
    string PasswordHash,
    string? DisplayName,
    UserRole Role,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
