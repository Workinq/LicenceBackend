namespace LicenceBackend.Api.Models.Response;

public sealed record SessionResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserResponse User
);
