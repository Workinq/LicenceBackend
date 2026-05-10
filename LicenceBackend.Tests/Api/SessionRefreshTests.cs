using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LicenceBackend.Tests.Api;

public sealed class SessionRefreshTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Refresh_returns_new_access_and_rotates_cookie()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var first = await LoginAsync(AdminEmail, AdminPassword);

        var response = await first.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(second);
        Assert.NotEqual(first.Payload.AccessToken, second.AccessToken);

        using var probe = NewClient(handleCookies: true);
        probe.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await probe.GetAsync("/me")).StatusCode);
    }

    [SkippableFact]
    public async Task Reusing_old_refresh_after_rotation_kills_entire_chain()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var first = await LoginAsync(AdminEmail, AdminPassword);
        var originalCookieValue = first.RefreshCookieValue;

        var rotate = await first.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);

        using var replay = NewClient(handleCookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", $"refresh_token={originalCookieValue}");
        var reuse = await replay.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var after = await first.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_with_unknown_token_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client = NewClient(handleCookies: false);
        client.DefaultRequestHeaders.Add("Cookie", "refresh_token=not-a-real-refresh-token");
        var response = await client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_revokes_current_refresh_only()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var firstLogin = await LoginAsync(AdminEmail, AdminPassword);
        using var secondLogin = await LoginAsync(AdminEmail, AdminPassword);

        firstLogin.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstLogin.Payload.AccessToken);
        var logout = await firstLogin.Client.DeleteAsync("/sessions");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The refresh token has been DB-revoked by logout; the cookie is still in the container
        // but the server rejects it. (Cookie clearing via Set-Cookie is added in Task 8.)
        var dead = await firstLogin.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);

        var alive = await secondLogin.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_all_revokes_every_refresh_for_user()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "logout-all@test.local";
        var password = "logout-all-pw-12345";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var loginA = await LoginAsync(email, password);
        using var loginB = await LoginAsync(email, password);

        loginA.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA.Payload.AccessToken);
        var logoutAll = await loginA.Client.DeleteAsync("/sessions/all");
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);

        var aDead = await loginA.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, aDead.StatusCode);

        var bDead = await loginB.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, bDead.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_concurrent_uses_revoke_all_user_refreshes()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "race-refresh@test.local";
        var password = "race-refresh-pw-12345";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        using var login = await LoginAsync(email, password);
        var cookieValue = login.RefreshCookieValue;

        const int parallel = 5;
        var tasks = Enumerable.Range(0, parallel).Select(_ =>
        {
            var c = NewClient(handleCookies: false);
            c.DefaultRequestHeaders.Add("Cookie", $"refresh_token={cookieValue}");
            return c.PostAsync("/sessions/refresh", content: null).ContinueWith(t =>
            {
                c.Dispose();
                return t.Result;
            });
        }).ToArray();
        var responses = await Task.WhenAll(tasks);

        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthorised = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);
        Assert.Equal(1, ok);
        Assert.Equal(parallel - 1, unauthorised);

        // Cascade check: the original cookie value must now be dead even on the winner's chain
        // (because the loser race triggers RevokeAllForUserAsync).
        using var probe = NewClient(handleCookies: false);
        probe.DefaultRequestHeaders.Add("Cookie", $"refresh_token={cookieValue}");
        var probeResponse = await probe.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, probeResponse.StatusCode);

        foreach (var response in responses) response.Dispose();
    }

    [SkippableFact]
    public async Task Suspending_user_revokes_every_refresh()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "suspend-revokes@test.local";
        var password = "suspend-revokes-pw-12345";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var user = await create.Content.ReadFromJsonAsync<UserMini>();
        Assert.NotNull(user);

        using var login = await LoginAsync(email, password);

        var suspend = await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "suspended", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var dead = await login.Client.PostAsync("/sessions/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);
    }

    private async Task<LoggedInSession> LoginAsync(string email, string password)
    {
        var client = NewClient(handleCookies: true);
        var response = await client.PostAsJsonAsync("/sessions", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(payload);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token=", StringComparison.Ordinal));
        var rawValue = setCookie["refresh_token=".Length..setCookie.IndexOf(';')];

        return new LoggedInSession(client, payload, rawValue);
    }

    private HttpClient NewClient(bool handleCookies)
    {
        return Factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies
        });
    }

    private sealed record LoggedInSession(HttpClient Client, SessionPayload Payload, string RefreshCookieValue) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed record SessionPayload(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        UserMini User
    );

    private sealed record UserMini(Guid Id, string Email, string Role, string Status);
}
