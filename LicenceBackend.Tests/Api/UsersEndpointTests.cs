using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class UsersEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Admin_creates_user_returns_201_without_password()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/users", new { email = "bob@test.local", password = "bob-password-12345", displayName = "Bob", role = "user" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Equal("bob@test.local", body.Email);
        Assert.Equal("Bob", body.DisplayName);
        Assert.Equal("user", body.Role);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Create_user_with_duplicate_email_returns_409()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var first = await AuthedClient.PostAsJsonAsync("/users", new { email = "dup@test.local", password = "dup-password-12345", role = "user" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await AuthedClient.PostAsJsonAsync("/users", new { email = "DUP@test.local", password = "other-password-12345", role = "user" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [SkippableFact]
    public async Task Create_user_with_short_password_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/users", new { email = "short@test.local", password = "short", role = "user" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Create_user_with_invalid_role_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/users", new { email = "role@test.local", password = "role-password-12345", role = "root" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task List_users_returns_paged_result_with_seeded_admin()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync("/users?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedUsersPayload>();
        Assert.NotNull(body);
        Assert.True(body.Total >= 1);
        Assert.Contains(body.Items, u => u.Email == AdminEmail);
    }

    [SkippableFact]
    public async Task GetById_returns_user()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync($"/users/{AdminUserId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Equal(AdminEmail, body.Email);
        Assert.Equal("admin", body.Role);
    }

    [SkippableFact]
    public async Task GetById_unknown_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync($"/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Me_returns_current_user_profile()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Equal(AdminUserId, body.Id);
        Assert.Equal(AdminEmail, body.Email);
        Assert.Equal("admin", body.Role);
    }

    private sealed record UserPayload(Guid Id, string Email, string? DisplayName, string Role, DateTimeOffset CreatedAt);

    private sealed record PagedUsersPayload(IReadOnlyList<UserPayload> Items, int Total, int Limit, int Offset);
}
