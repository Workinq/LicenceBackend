using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class LicencesEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Post_with_userId_creates_licence_and_returns_raw_key_once()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("app-pro", "App Pro");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = product.Id, userId = AdminUserId, notes = "integration test" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(product.Id, body.ProductId);
        Assert.Equal("app-pro", body.ProductSlug);
        Assert.Equal(AdminUserId, body.UserId);
        Assert.Equal(AdminEmail, body.UserEmail);
        Assert.Equal("active", body.Status);
        Assert.Equal("integration test", body.Notes);
        Assert.Null(body.ExpiresAt);
        Assert.False(string.IsNullOrWhiteSpace(body.LicenceKey));
        Assert.StartsWith("LIC-", body.LicenceKey);

        var getResponse = await AuthedClient.GetAsync($"/licences/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var rawJson = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("licenceKey", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Post_with_email_resolves_owner()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("email-owner", "Email Owner");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = product.Id, email = AdminEmail });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        Assert.Equal(AdminUserId, body.UserId);
        Assert.Equal(AdminEmail, body.UserEmail);
    }

    [SkippableFact]
    public async Task Post_without_owner_returns_400_missing_owner()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("no-owner", "No Owner");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = product.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("missing_owner", json);
    }

    [SkippableFact]
    public async Task Post_with_both_owners_returns_400_ambiguous_owner()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("ambiguous", "Ambiguous");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = product.Id, userId = AdminUserId, email = AdminEmail });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("ambiguous_owner", json);
    }

    [SkippableFact]
    public async Task Post_with_unknown_user_returns_400_owner_not_found()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("unknown-user", "Unknown User");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = product.Id, userId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("owner_not_found", json);
    }

    [SkippableFact]
    public async Task Post_with_unknown_product_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { productId = Guid.NewGuid(), userId = AdminUserId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Post_without_productId_is_rejected_at_deserialization()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new { userId = AdminUserId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Post_with_past_expires_at_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("expiring", "Expiring");

        var response = await AuthedClient.PostAsJsonAsync(
                           "/licences",
                           new
                           {
                               productId = product.Id,
                               userId = AdminUserId,
                               expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                           });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Created_licence_is_usable_via_verify_endpoint()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("usable", "Usable");
        var created = await CreateLicenceAsync(product.Id);

        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync(
                           "/licences/verify",
                           new { licenceKey = created.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task List_filters_by_product_user_and_paginates()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var productA = await CreateProductAsync("prod-a", "A");
        var productB = await CreateProductAsync("prod-b", "B");

        for (var i = 0; i < 3; i++) _ = await CreateLicenceAsync(productA.Id);
        for (var i = 0; i < 2; i++) _ = await CreateLicenceAsync(productB.Id);

        var all = await AuthedClient.GetFromJsonAsync<PagedLicencesPayload>("/licences");
        Assert.NotNull(all);
        Assert.Equal(5, all.Total);

        var onlyA = await AuthedClient.GetFromJsonAsync<PagedLicencesPayload>($"/licences?productId={productA.Id}");
        Assert.NotNull(onlyA);
        Assert.Equal(3, onlyA.Total);
        Assert.All(onlyA.Items, item => Assert.Equal(productA.Id, item.ProductId));
        Assert.All(onlyA.Items, item => Assert.Null(item.LicenceKey));

        var byUser = await AuthedClient.GetFromJsonAsync<PagedLicencesPayload>($"/licences?userId={AdminUserId}");
        Assert.NotNull(byUser);
        Assert.Equal(5, byUser.Total);

        var paged = await AuthedClient.GetFromJsonAsync<PagedLicencesPayload>("/licences?limit=2&offset=0");
        Assert.NotNull(paged);
        Assert.Equal(5, paged.Total);
        Assert.Equal(2, paged.Items.Count);
        Assert.Equal(2, paged.Limit);
    }

    [SkippableFact]
    public async Task List_with_invalid_status_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync("/licences?status=bogus");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetById_unknown_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/licences/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Me_licences_returns_only_caller_licences()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-test", "Me Test");

        // Admin-owned licence
        _ = await CreateLicenceAsync(product.Id);

        // Create a regular user + one licence they own
        var regularEmail = "regular-me@test.local";
        var regularPassword = "regular-me-password-12345";
        var createUser = await AuthedClient.PostAsJsonAsync(
                             "/users",
                             new { email = regularEmail, password = regularPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        var regularLic = await AuthedClient.PostAsJsonAsync(
                             "/licences",
                             new { productId = product.Id, email = regularEmail, notes = "regular owned" });
        Assert.Equal(HttpStatusCode.Created, regularLic.StatusCode);

        using var regularClient = await CreateLoggedInClientAsync(regularEmail, regularPassword);
        var mine = await regularClient.GetFromJsonAsync<PagedLicencesPayload>("/me/licences");
        Assert.NotNull(mine);
        Assert.Equal(1, mine.Total);
        Assert.All(mine.Items, item => Assert.Equal(regularEmail, item.UserEmail));
        Assert.All(mine.Items, item => Assert.Null(item.LicenceKey));
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<LicenceCreatedPayload> CreateLicenceAsync(Guid productId)
    {
        var response = await AuthedClient.PostAsJsonAsync("/licences", new { productId, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        return body;
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicenceCreatedPayload(
        Guid Id,
        Guid ProductId,
        string ProductSlug,
        Guid UserId,
        string UserEmail,
        string Status,
        DateTimeOffset? ExpiresAt,
        string? Notes,
        DateTimeOffset CreatedAt,
        string LicenceKey
    );

    private sealed record LicencePayload(
        Guid Id,
        Guid ProductId,
        string ProductSlug,
        Guid UserId,
        string UserEmail,
        string Status,
        DateTimeOffset? ExpiresAt,
        string? Notes,
        DateTimeOffset CreatedAt,
        string? LicenceKey
    );

    private sealed record PagedLicencesPayload(IReadOnlyList<LicencePayload> Items, int Total, int Limit, int Offset);
}
