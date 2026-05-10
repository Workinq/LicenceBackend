using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceStateTransitionTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Suspend_then_revoke_then_reinstate_works_and_is_audited()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceId, licenceKey) = await CreateProductAndLicenceAsync("transition");
        await AssertVerifyEndpoint(HttpStatusCode.OK, productId, licenceKey);

        // Suspend
        var suspend = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "suspended", reason = "pause" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspended = await suspend.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.Equal("suspended", suspended!.Status);

        await AssertVerifyEndpoint(HttpStatusCode.BadRequest, productId, licenceKey);

        // Revoke
        var revoke = await AuthedClient.PatchAsJsonAsync(
                         $"/licences/{licenceId}/status",
                         new { status = "revoked", reason = "cancelled" });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var revoked = await revoke.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.Equal("revoked", revoked!.Status);

        await AssertVerifyEndpoint(HttpStatusCode.BadRequest, productId, licenceKey);

        // Reinstate (free transitions)
        var reinstate = await AuthedClient.PatchAsJsonAsync(
                            $"/licences/{licenceId}/status",
                            new { status = "active" });
        Assert.Equal(HttpStatusCode.OK, reinstate.StatusCode);
        var reinstated = await reinstate.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.Equal("active", reinstated!.Status);

        await AssertVerifyEndpoint(HttpStatusCode.OK, productId, licenceKey);

        // History: 3 entries, newest first, with changed_by + email populated
        var history = await AuthedClient.GetFromJsonAsync<PagedHistoryPayload>(
                          $"/licences/{licenceId}/status-history");
        Assert.NotNull(history);
        Assert.Equal(3, history.Total);
        Assert.Equal("active", history.Items[0].NewStatus);
        Assert.Equal("revoked", history.Items[0].PreviousStatus);
        Assert.Equal("revoked", history.Items[1].NewStatus);
        Assert.Equal("suspended", history.Items[2].NewStatus);
        Assert.Equal("pause", history.Items[2].Reason);
        Assert.Equal("cancelled", history.Items[1].Reason);
        Assert.All(history.Items, h => Assert.Equal(AdminUserId, h.ChangedBy));
        Assert.All(history.Items, h => Assert.Equal(AdminEmail, h.ChangedByEmail));
    }

    [SkippableFact]
    public async Task Patching_same_status_is_idempotent_and_writes_no_history()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, licenceId, _) = await CreateProductAndLicenceAsync("idempotent");

        var response = await AuthedClient.PatchAsJsonAsync(
                           $"/licences/{licenceId}/status",
                           new { status = "active" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await AuthedClient.GetFromJsonAsync<PagedHistoryPayload>(
                          $"/licences/{licenceId}/status-history");
        Assert.NotNull(history);
        Assert.Equal(0, history.Total);
    }

    [SkippableFact]
    public async Task Patching_unknown_licence_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PatchAsJsonAsync(
                           $"/licences/{Guid.NewGuid()}/status",
                           new { status = "suspended" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Patching_with_invalid_status_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, licenceId, _) = await CreateProductAndLicenceAsync("invalid-status");
        var response = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "expired" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Status_history_for_unknown_licence_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/licences/{Guid.NewGuid()}/status-history");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_admin_cannot_patch_status()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, licenceId, _) = await CreateProductAndLicenceAsync("non-admin");

        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = "transitions-user@test.local", password = "transitions-pw-12345", role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        using var regular = await CreateLoggedInClientAsync("transitions-user@test.local", "transitions-pw-12345");
        var attempt = await regular.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "suspended" });

        Assert.Equal(HttpStatusCode.Forbidden, attempt.StatusCode);
    }

    [SkippableFact]
    public async Task Unauthenticated_patch_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, licenceId, _) = await CreateProductAndLicenceAsync("unauth");
        var response = await UnauthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "suspended" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_sees_suspended_licence_in_me_licences()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var email = "suspended-viewer@test.local";
        var password = "suspended-viewer-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug = "visible-suspended", displayName = "Visible Suspended" });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product!.Id, email });
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicenceCreatedPayload>();

        // Suspend the licence
        var suspend = await AuthedClient.PatchAsJsonAsync($"/licences/{licence!.Id}/status", new { status = "suspended", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        // Owner can still see it
        using var ownerClient = await CreateLoggedInClientAsync(email, password);
        var mine = await ownerClient.GetFromJsonAsync<PagedLicencesPayload>("/me/licences");
        Assert.NotNull(mine);
        Assert.Equal(1, mine.Total);
        Assert.Equal("suspended", mine.Items[0].Status);
    }

    private async Task<(Guid productId, Guid licenceId, string licenceKey)> CreateProductAndLicenceAsync(string slug)
    {
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(licence);

        return (product.Id, licence.Id, licence.LicenceKey);
    }

    private async Task AssertVerifyEndpoint(HttpStatusCode expected, Guid productId, string licenceKey)
    {
        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(expected, response.StatusCode);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicenceCreatedPayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record LicencePayload(
        Guid Id,
        Guid ProductId,
        string ProductSlug,
        Guid UserId,
        string UserEmail,
        string Status,
        DateTimeOffset? ExpiresAt,
        string? Notes,
        DateTimeOffset CreatedAt
    );

    private sealed record PagedLicencesPayload(IReadOnlyList<LicencePayload> Items, int Total, int Limit, int Offset);

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
