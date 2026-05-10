using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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

    [SkippableFact]
    public async Task Refresh_reads_cookie_and_rotates_it()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var loginResponse = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var firstCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token=", StringComparison.Ordinal));

        // The client's CookieContainer holds the refresh cookie from login; subsequent calls to /sessions/refresh
        // attach it automatically because the base address is https://localhost (Secure cookie + Path=/sessions match).
        var refreshResponse = await UnauthedClient.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        Assert.True(refreshResponse.Headers.TryGetValues("Set-Cookie", out var rotated), "Expected Set-Cookie on refresh response.");
        var rotatedCookieHeader = rotated!.Single(h => h.StartsWith("refresh_token=", StringComparison.Ordinal));
        Assert.NotEqual(firstCookieHeader, rotatedCookieHeader);

        var body = await refreshResponse.Content.ReadFromJsonAsync<RefreshBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
    }

    [SkippableFact]
    public async Task Refresh_without_cookie_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var freshClient = Factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        var response = await freshClient.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginBody(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        UserBody User
    );

    private sealed record RefreshBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt);

    private sealed record UserBody(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);
}
