namespace LicenceBackend.Api.Models.Response;

public sealed record SessionResponse(
    string         AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string         RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserResponse   User
);
