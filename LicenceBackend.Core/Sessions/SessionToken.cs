namespace LicenceBackend.Core.Sessions;

public sealed record SessionToken(
    string Token,
    DateTimeOffset ExpiresAt
);
