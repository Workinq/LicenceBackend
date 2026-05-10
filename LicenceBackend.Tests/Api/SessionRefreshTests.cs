using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class SessionRefreshTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Refresh_returns_new_access_and_refresh_tokens()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var first = await LoginAsync(AdminEmail, AdminPassword);

        var response = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var second = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(second);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me")).StatusCode);
    }

    [SkippableFact]
    public async Task Reusing_old_refresh_after_rotation_kills_entire_chain()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var first = await LoginAsync(AdminEmail, AdminPassword);

        // Rotate once
        var rotate = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", first.RefreshToken);
        var second = await rotate.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(second);

        // Try reuse the original refresh
        var reuse = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // The second refresh should be dead
        var after = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", second.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_with_unknown_token_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", "not-a-real-refresh-token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_revokes_current_refresh_only()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        // Login twice
        var firstLogin = await LoginAsync(AdminEmail, AdminPassword);
        var secondLogin = await LoginAsync(AdminEmail, AdminPassword);

        using var firstClient = Factory!.CreateClient();
        firstClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstLogin.AccessToken);

        var logout = await firstClient.DeleteAsync("/sessions");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Logged-out refresh is dead
        var dead = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", firstLogin.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);

        // Second refresh is unaffected
        var alive = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", secondLogin.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_all_revokes_every_refresh_for_user()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        // Create a regular user so we don't stomp on the shared admin client
        var email = "logout-all@test.local";
        var password = "logout-all-pw-12345";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var loginA = await LoginAsync(email, password);
        var loginB = await LoginAsync(email, password);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginA.AccessToken);
        var logoutAll = await client.DeleteAsync("/sessions/all");
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);

        var aDead = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", loginA.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, aDead.StatusCode);

        var bDead = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", loginB.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, bDead.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_concurrent_uses_revoke_all_user_refreshes()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        // Use a dedicated user so we don't interfere with the shared admin's session state
        var email = "race-refresh@test.local";
        var password = "race-refresh-pw-12345";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var login = await LoginAsync(email, password);

        const int parallel = 5;
        var tasks = Enumerable.Range(0, parallel).Select(_ => UnauthedClient.PostAsJsonAsync("/sessions/refresh", login.RefreshToken)).ToArray();
        var responses = await Task.WhenAll(tasks);

        var ok = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthorised = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);
        Assert.Equal(1, ok);
        Assert.Equal(parallel - 1, unauthorised);

        var winnerResponse = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
        var winnerSession = await winnerResponse.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(winnerSession);

        var afterCascade = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", winnerSession.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterCascade.StatusCode);

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

        var login = await LoginAsync(email, password);

        var suspend = await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "suspended", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var dead = await UnauthedClient.PostAsJsonAsync("/sessions/refresh", login.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);
    }

    private async Task<SessionPayload> LoginAsync(string email, string password)
    {
        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(payload);
        return payload;
    }

    private sealed record SessionPayload(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt,
        UserMini User
    );

    private sealed record UserMini(Guid Id, string Email, string Role, string Status);
}
