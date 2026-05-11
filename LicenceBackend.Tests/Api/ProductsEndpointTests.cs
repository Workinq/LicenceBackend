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

    [SkippableFact]
    public async Task Post_with_extra_fields_round_trips()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/products", new
        {
            slug = "extra-fields",
            displayName = "Extra",
            description = "Long copy.",
            tagline = "Short one.",
            isPublic = false,
            price = 9.99m,
            currency = "EUR",
            sortOrder = 3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        Assert.Equal("Long copy.", body.Description);
        Assert.Equal("Short one.", body.Tagline);
        Assert.False(body.IsPublic);
        Assert.Equal(9.99m, body.Price);
        Assert.Equal("EUR", body.Currency);
        Assert.Equal(3, body.SortOrder);
        Assert.Null(body.ImageUrl);
    }

    [SkippableFact]
    public async Task Post_with_invalid_currency_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug = "bad-cur", displayName = "X", currency = "usd" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_updates_fields_and_keeps_slug()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = "patch-me", displayName = "Original" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(created);

        var patch = await AuthedClient.PatchAsJsonAsync($"/products/{created.Id}", new { displayName = "Renamed", isPublic = false, price = 12.50m }, cancellationToken: default);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated.DisplayName);
        Assert.False(updated.IsPublic);
        Assert.Equal(12.50m, updated.Price);
        Assert.Equal("patch-me", updated.Slug);
    }

    [SkippableFact]
    public async Task Patch_unknown_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var patch = await AuthedClient.PatchAsJsonAsync($"/products/{Guid.NewGuid()}", new { displayName = "X" }, cancellationToken: default);

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
    }

    [SkippableFact]
    public async Task Upload_image_then_GetImage_streams_it()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = "image-upload", displayName = "Image Product" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(created);
        var id = created.Id;

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(png);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(part, "file", "logo.png");

        var uploadResponse = await AuthedClient.PostAsync($"/products/{id}/image", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(uploaded);
        Assert.Equal($"/products/{id}/image", uploaded.ImageUrl);

        var getResponse = await UnauthedClient.GetAsync($"/products/{id}/image");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        var bytes = await getResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.SequenceEqual(png));
    }

    [SkippableFact]
    public async Task Upload_image_with_bad_content_type_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = "image-badtype", displayName = "Bad Type Product" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(created);

        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(new byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F });
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(part, "file", "hello.txt");

        var response = await AuthedClient.PostAsync($"/products/{created.Id}/image", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Delete_image_clears_imageUrl()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var create = await AuthedClient.PostAsJsonAsync("/products", new { slug = "image-delete", displayName = "Delete Image Product" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(created);
        var id = created.Id;

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var uploadContent = new MultipartFormDataContent();
        var part = new ByteArrayContent(png);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        uploadContent.Add(part, "file", "logo.png");
        var uploadResponse = await AuthedClient.PostAsync($"/products/{id}/image", uploadContent);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var deleteResponse = await AuthedClient.DeleteAsync($"/products/{id}/image");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var afterDelete = await deleteResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(afterDelete);
        Assert.Null(afterDelete.ImageUrl);

        var getResponse = await UnauthedClient.GetAsync($"/products/{id}/image");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    private sealed record ProductPayload(
        Guid Id,
        string Slug,
        string DisplayName,
        string? Description,
        string? Tagline,
        bool IsPublic,
        decimal? Price,
        string Currency,
        int SortOrder,
        string? ImageUrl,
        DateTimeOffset CreatedAt
    );

    private sealed record PagedProductsPayload(IReadOnlyList<ProductPayload> Items, int Total, int Limit, int Offset);
}
