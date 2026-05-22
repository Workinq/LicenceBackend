using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class OrdersEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Me_orders_returns_only_callers_orders()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-orders", "Me Orders");

        await PlaceOrderAsync(AuthedClient, product.Id);

        var regularEmail = "regular-orders@test.local";
        var regularPassword = "regular-orders-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = regularEmail, password = regularPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        using var regular = await CreateLoggedInClientAsync(regularEmail, regularPassword);
        await PlaceOrderAsync(regular, product.Id);

        var mine = await regular.GetFromJsonAsync<PagedOrdersPayload>("/me/orders");
        Assert.NotNull(mine);
        Assert.Equal(1, mine.Total);
    }

    [SkippableFact]
    public async Task Me_orders_by_id_for_other_user_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("not-mine", "Not Mine");
        var order = await PlaceOrderAsync(AuthedClient, product.Id);

        var regularEmail = "regular-foreign@test.local";
        var regularPassword = "regular-foreign-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = regularEmail, password = regularPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        using var regular = await CreateLoggedInClientAsync(regularEmail, regularPassword);

        var resp = await regular.GetAsync($"/me/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_orders_requires_admin_role()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var regularEmail = "regular-admin-check@test.local";
        var regularPassword = "regular-admin-check-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = regularEmail, password = regularPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        using var regular = await CreateLoggedInClientAsync(regularEmail, regularPassword);

        var resp = await regular.GetAsync("/admin/orders");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_orders_lists_all_users_orders()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("admin-list", "Admin List");
        await PlaceOrderAsync(AuthedClient, product.Id);

        var regularEmail = "regular-admin-list@test.local";
        var regularPassword = "regular-admin-list-pw-12345";
        await AuthedClient.PostAsJsonAsync("/users", new { email = regularEmail, password = regularPassword, role = "user" });
        using var regular = await CreateLoggedInClientAsync(regularEmail, regularPassword);
        await PlaceOrderAsync(regular, product.Id);

        var all = await AuthedClient.GetFromJsonAsync<PagedOrdersPayload>("/admin/orders");
        Assert.NotNull(all);
        Assert.True(all.Total >= 2);
    }

    [SkippableFact]
    public async Task Me_orders_by_id_carries_label_from_purchase()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-carry", "Label Carry");
        var order = await PlaceOrderAsync(AuthedClient, product.Id, "Nickname");

        var fetched = await AuthedClient.GetFromJsonAsync<OrderPayload>($"/me/orders/{order.Id}");
        Assert.NotNull(fetched);
        Assert.Single(fetched.Items);
        Assert.Equal("Nickname", fetched.Items[0].Label);
        Assert.Null(fetched.Items[0].LicenceKey);
    }

    private async Task<OrderPayload> PlaceOrderAsync(HttpClient client, Guid productId, string? label = null)
    {
        var resp = await client.PostAsJsonAsync("/payments/checkout", new
        {
            contactEmail = (string?)null,
            items = new[]
            {
                new { productId, quantity = 1, labels = new[] { label } }
            }
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var checkout = await resp.Content.ReadFromJsonAsync<CheckoutSessionPayload>();
        Assert.NotNull(checkout);
        Assert.True(checkout.Free);
        Assert.NotNull(checkout.OrderId);

        var order = await client.GetFromJsonAsync<OrderPayload>($"/me/orders/{checkout.OrderId}");
        Assert.NotNull(order);
        return order;
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name, isPublic = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

    private sealed record CheckoutSessionPayload(
        string? ClientSecret, Guid? CheckoutAttemptId, Guid? OrderId, bool Free);

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record OrderPayload(
        Guid Id,
        Guid UserId,
        string ContactEmail,
        string Status,
        DateTimeOffset CreatedAt,
        IReadOnlyList<CurrencyTotalPayload> Totals,
        IReadOnlyList<OrderItemPayload> Items
    );

    private sealed record CurrencyTotalPayload(string Currency, decimal Amount);

    private sealed record OrderItemPayload(
        Guid Id,
        Guid ProductId,
        string ProductSlug,
        string ProductDisplayName,
        Guid LicenceId,
        string? Label,
        decimal? UnitPrice,
        string Currency,
        string? LicenceKey
    );

    private sealed record PagedOrdersPayload(IReadOnlyList<OrderPayload> Items, int Total, int Limit, int Offset);
}
