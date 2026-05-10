using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class UserSuspensionTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Suspending_user_blocks_login_and_tokens_and_reactivation_restores_access()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "suspend-target@test.local";
        var password = "suspend-target-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        var user = await createUser.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(user);

        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug = "suspendable", displayName = "Suspendable" });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = user.Id });
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        using (var targetClient = await CreateLoggedInClientAsync(email, password))
        {
            Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/me")).StatusCode);
        }

        await AssertVerifyEndpoint(HttpStatusCode.OK, product.Id, licence.LicenceKey);

        // Suspend the user
        var suspend = await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "suspended", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspendedPayload = await suspend.Content.ReadFromJsonAsync<UserPayload>();
        Assert.Equal("suspended", suspendedPayload!.Status);

        // Login refused
        var reloginAttempt = await UnauthedClient.PostAsJsonAsync("/sessions", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, reloginAttempt.StatusCode);
        var reloginBody = await reloginAttempt.Content.ReadAsStringAsync();
        Assert.Contains("account_suspended", reloginBody);

        // /licences/verify refuses the suspended owner's licence
        await AssertVerifyEndpoint(HttpStatusCode.BadRequest, product.Id, licence.LicenceKey);

        // Reactivate
        var reactivate = await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "active" });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        // Login works again
        using (var reAuthed = await CreateLoggedInClientAsync(email, password))
        {
            Assert.Equal(HttpStatusCode.OK, (await reAuthed.GetAsync("/me")).StatusCode);
        }

        await AssertVerifyEndpoint(HttpStatusCode.OK, product.Id, licence.LicenceKey);
    }

    [SkippableFact]
    public async Task Admin_cannot_suspend_self()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync($"/users/{AdminUserId}/status", new { status = "suspended", reason = "nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cannot_suspend_self", body);
    }

    [SkippableFact]
    public async Task Suspending_same_status_is_idempotent_and_writes_no_history()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "idempotent@test.local";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password = "idempotent-pw-12345", role = "user" });
        var user = await create.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(user);

        var reactivate = await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "active" });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        var history = await AuthedClient.GetFromJsonAsync<PagedHistoryPayload>($"/users/{user.Id}/status-history");
        Assert.NotNull(history);
        Assert.Equal(0, history.Total);
    }

    [SkippableFact]
    public async Task Status_history_records_transitions_with_actor_and_reason()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "history@test.local";
        var create = await AuthedClient.PostAsJsonAsync("/users", new { email, password = "history-pw-12345", role = "user" });
        var user = await create.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(user);

        await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "suspended", reason = "billing" });
        await AuthedClient.PatchAsJsonAsync($"/users/{user.Id}/status", new { status = "active" });

        var history = await AuthedClient.GetFromJsonAsync<PagedHistoryPayload>($"/users/{user.Id}/status-history");

        Assert.NotNull(history);
        Assert.Equal(2, history.Total);
        Assert.Equal("active", history.Items[0].NewStatus);
        Assert.Equal("suspended", history.Items[0].PreviousStatus);
        Assert.Equal("suspended", history.Items[1].NewStatus);
        Assert.Equal("billing", history.Items[1].Reason);
        Assert.All(history.Items, h => Assert.Equal(AdminUserId, h.ChangedBy));
        Assert.All(history.Items, h => Assert.Equal(AdminEmail, h.ChangedByEmail));
    }

    [SkippableFact]
    public async Task Status_history_for_unknown_user_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/users/{Guid.NewGuid()}/status-history");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task AssertVerifyEndpoint(HttpStatusCode expected, Guid productId, string licenceKey)
    {
        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(expected, response.StatusCode);
    }

    private sealed record UserPayload(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record HistoryPayload(
        Guid Id,
        string PreviousStatus,
        string NewStatus,
        Guid ChangedBy,
        string? ChangedByEmail,
        DateTimeOffset ChangedAt,
        string? Reason
    );

    private sealed record PagedHistoryPayload(IReadOnlyList<HistoryPayload> Items, int Total, int Limit, int Offset);
}
