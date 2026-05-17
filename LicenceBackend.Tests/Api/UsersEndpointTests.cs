using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

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
    public async Task Create_user_ignores_role_field_and_always_creates_role_user()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/users", new { email = "role@test.local", password = "role-password-12345", role = "admin" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Equal("user", body.Role);
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
    public async Task List_filters_by_q_case_insensitively_on_email()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        await AuthedClient.PostAsJsonAsync("/users", new { email = "alice@acme.test", password = "alice-password-12345" });
        await AuthedClient.PostAsJsonAsync("/users", new { email = "bob@acme.test", password = "bob-password-12345" });
        await AuthedClient.PostAsJsonAsync("/users", new { email = "alice.h@other.test", password = "alice-h-password-12345" });

        var response = await AuthedClient.GetAsync("/users?q=ALICE");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedUsersPayload>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Total);
        Assert.All(body.Items, u => Assert.Contains("alice", u.Email, StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task List_filters_by_role()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        await AuthedClient.PostAsJsonAsync("/users", new { email = "regular1@test.local", password = "regular1-password-12345" });
        await AuthedClient.PostAsJsonAsync("/users", new { email = "regular2@test.local", password = "regular2-password-12345" });

        var adminsResponse = await AuthedClient.GetAsync("/users?role=admin");
        Assert.Equal(HttpStatusCode.OK, adminsResponse.StatusCode);
        var admins = await adminsResponse.Content.ReadFromJsonAsync<PagedUsersPayload>();
        Assert.NotNull(admins);
        Assert.All(admins.Items, u => Assert.Equal("admin", u.Role));
        Assert.Contains(admins.Items, u => u.Email == AdminEmail);

        var usersResponse = await AuthedClient.GetAsync("/users?role=user");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var nonAdmins = await usersResponse.Content.ReadFromJsonAsync<PagedUsersPayload>();
        Assert.NotNull(nonAdmins);
        Assert.Equal(2, nonAdmins.Total);
        Assert.All(nonAdmins.Items, u => Assert.Equal("user", u.Role));
    }

    [SkippableFact]
    public async Task List_filters_by_status()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/users", new { email = "suspended@test.local", password = "suspended-password-12345" });
        var created = await create.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(created);

        var suspend = await AuthedClient.PatchAsJsonAsync($"/users/{created.Id}/status", new { status = "suspended" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        var response = await AuthedClient.GetAsync("/users?status=suspended");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedUsersPayload>();
        Assert.NotNull(body);
        Assert.Equal(1, body.Total);
        Assert.Equal(created.Id, body.Items[0].Id);
    }

    [SkippableFact]
    public async Task List_with_invalid_role_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync("/users?role=root");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task List_with_invalid_status_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.GetAsync("/users?status=deleted");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
    public async Task Patch_me_updates_display_name()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync("/me", new { displayName = "Updated Name" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Equal("Updated Name", body.DisplayName);

        var followUp = await AuthedClient.GetAsync("/me");
        var refreshed = await followUp.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(refreshed);
        Assert.Equal("Updated Name", refreshed.DisplayName);
    }

    [SkippableFact]
    public async Task Patch_me_clears_display_name_when_null()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        await AuthedClient.PatchAsJsonAsync("/me", new { displayName = "Temporary" });

        var response = await AuthedClient.PatchAsJsonAsync("/me", new { displayName = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Null(body.DisplayName);
    }

    [SkippableFact]
    public async Task Patch_me_trims_whitespace_to_null()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync("/me", new { displayName = "   " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        Assert.Null(body.DisplayName);
    }

    [SkippableFact]
    public async Task Patch_me_requires_auth()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var unauthed = Factory!.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = HttpsBaseAddress, HandleCookies = true });
        var response = await unauthed.PatchAsJsonAsync("/me", new { displayName = "Hi" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_me_password_changes_password_and_revokes_refreshes()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var newPassword = "new-admin-pw-12345";
        var change = await AuthedClient.PatchAsJsonAsync("/me/password", new { currentPassword = AdminPassword, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var oldLogin = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_me_password_with_wrong_current_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync(
            "/me/password",
            new { currentPassword = "wrong-current-pw-12345", newPassword = "next-admin-pw-12345" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_me_password_too_short_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync(
            "/me/password",
            new { currentPassword = AdminPassword, newPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_me_password_requires_auth()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var unauthed = Factory!.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = HttpsBaseAddress, HandleCookies = true });
        var response = await unauthed.PatchAsJsonAsync("/me/password", new { currentPassword = "x", newPassword = "next-pw-12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
