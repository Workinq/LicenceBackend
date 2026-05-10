using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class SessionRefreshCookieTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Login_sets_refresh_cookie_with_strict_attributes_and_omits_token_from_body()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders), "Expected Set-Cookie header on login response.");
        var cookieHeader = cookieHeaders!.Single(h => h.StartsWith("refresh_token=", StringComparison.Ordinal));
        Assert.Contains("HttpOnly", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/sessions", cookieHeader, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(AdminEmail, body.User.Email);
    }

    private sealed record LoginBody(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        UserBody User
    );

    private sealed record UserBody(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);
}
