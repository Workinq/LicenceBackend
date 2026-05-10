using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class ProductsEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Post_creates_product_and_returns_201()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug = "app-pro", displayName = "App Pro" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("app-pro", body.Slug);
        Assert.Equal("App Pro", body.DisplayName);
    }

    [SkippableFact]
    public async Task Post_with_duplicate_slug_returns_409()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var first = await AuthedClient.PostAsJsonAsync("/products", new { slug = "dup", displayName = "First" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await AuthedClient.PostAsJsonAsync("/products", new { slug = "dup", displayName = "Second" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [SkippableFact]
    public async Task Post_with_invalid_slug_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug = "Not Valid!", displayName = "Nope" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task List_returns_paged_products()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        for (var i = 0; i < 3; i++)
        {
            var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = $"product-{i}", displayName = $"Product {i}" });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var response = await AuthedClient.GetAsync("/products?limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedProductsPayload>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Total);
        Assert.Equal(3, body.Items.Count);
        Assert.Equal(10, body.Limit);
        Assert.Equal(0, body.Offset);
    }

    [SkippableFact]
    public async Task GetById_returns_the_product()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = "single", displayName = "Single" });
        var created = await create.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(created);

        var response = await AuthedClient.GetAsync($"/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("single", fetched.Slug);
    }

    [SkippableFact]
    public async Task GetById_unknown_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/products/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record PagedProductsPayload(IReadOnlyList<ProductPayload> Items, int Total, int Limit, int Offset);
}
