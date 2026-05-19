using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class OrdersEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Post_creates_order_with_one_licence_per_unit_and_returns_raw_keys()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("checkout-product", "Checkout Product", 25.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Equal(AdminUserId, body.UserId);
        Assert.Equal(AdminEmail, body.ContactEmail);
        Assert.Equal("completed", body.Status);
        Assert.Single(body.Items);
        Assert.Single(body.Totals);
        Assert.Equal("USD", body.Totals[0].Currency);
        Assert.Equal(25.00m, body.Totals[0].Amount);

        var item = body.Items[0];
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(product.Slug, item.ProductSlug);
        Assert.Equal(25.00m, item.UnitPrice);
        Assert.Equal("USD", item.Currency);
        Assert.Null(item.Label);
        Assert.False(string.IsNullOrWhiteSpace(item.LicenceKey));
        Assert.StartsWith("LIC-", item.LicenceKey!);
    }

    [SkippableFact]
    public async Task Post_with_quantity_three_creates_three_licences_each_with_label()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("multi-qty", "Multi Qty", 10.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new
                {
                    productId = product.Id,
                    quantity = 3,
                    labels = new[] { "Dev box", "CI server", "Staging" }
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Items.Count);
        Assert.Equal(new[] { "Dev box", "CI server", "Staging" }, body.Items.Select(i => i.Label).ToArray());
        Assert.Equal(3, body.Items.Select(i => i.LicenceKey).Distinct().Count());
        Assert.Single(body.Totals);
        Assert.Equal(30.00m, body.Totals[0].Amount);
    }

    [SkippableFact]
    public async Task Post_with_mixed_currencies_returns_per_currency_totals()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var usd = await CreateProductAsync("mixed-usd", "Mixed USD", 20.00m, "USD");
        var eur = await CreateProductAsync("mixed-eur", "Mixed EUR", 15.00m, "EUR");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = usd.Id, quantity = 2, labels = new string?[] { null, null } },
                new { productId = eur.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Equal(3, body.Items.Count);
        Assert.Equal(2, body.Totals.Count);
        var eurTotal = Assert.Single(body.Totals, t => t.Currency == "EUR");
        var usdTotal = Assert.Single(body.Totals, t => t.Currency == "USD");
        Assert.Equal(15.00m, eurTotal.Amount);
        Assert.Equal(40.00m, usdTotal.Amount);
    }

    [SkippableFact]
    public async Task Post_with_free_product_persists_order_with_zero_totals_and_null_unit_price()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var free = await CreateProductAsync("freebie", "Freebie");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = free.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Null(body.Items[0].UnitPrice);
        Assert.Single(body.Totals);
        Assert.Equal("USD", body.Totals[0].Currency);
        Assert.Equal(0m, body.Totals[0].Amount);
    }

    [SkippableFact]
    public async Task Post_without_contact_email_defaults_to_account_email()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("default-email", "Default Email", 5.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Equal(AdminEmail, body.ContactEmail);
    }

    [SkippableFact]
    public async Task Post_with_explicit_contact_email_uses_it()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("custom-email", "Custom Email", 5.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            contactEmail = "billing@acme.com",
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        Assert.Equal("billing@acme.com", body.ContactEmail);
    }

    [SkippableFact]
    public async Task Post_with_empty_items_returns_400_empty_order()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.PostAsJsonAsync("/orders", new { items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("empty_order", json);
    }

    [SkippableFact]
    public async Task Post_with_quantity_zero_returns_400_invalid_quantity()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("qty-zero", "Qty Zero", 5.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 0, labels = Array.Empty<string?>() }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_quantity", json);
    }

    [SkippableFact]
    public async Task Post_with_label_count_mismatch_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-mismatch", "Label Mismatch", 5.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 2, labels = new[] { "only-one" } }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("label_count_mismatch", json);
    }

    [SkippableFact]
    public async Task Post_with_label_too_long_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-long", "Label Long", 5.00m, "USD");
        var hugeLabel = new string('x', 11);

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new[] { hugeLabel } }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("label_too_long", json);
    }

    [SkippableFact]
    public async Task Post_with_invalid_contact_email_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("bad-email", "Bad Email", 5.00m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            contactEmail = "not-an-email",
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_contact_email", json);
    }

    [SkippableFact]
    public async Task Post_with_non_public_product_returns_403_and_persists_nothing()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("private-prod", "Private", 5.00m, "USD", isPublic: false);
        var ordersBefore = await CountAsync("orders");
        var licencesBefore = await CountAsync("licences");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("product_not_purchasable", json);
        Assert.Equal(ordersBefore, await CountAsync("orders"));
        Assert.Equal(licencesBefore, await CountAsync("licences"));
    }

    [SkippableFact]
    public async Task Post_with_unknown_product_returns_404_and_persists_nothing()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var ordersBefore = await CountAsync("orders");
        var licencesBefore = await CountAsync("licences");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = Guid.NewGuid(), quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("product_not_found", json);
        Assert.Equal(ordersBefore, await CountAsync("orders"));
        Assert.Equal(licencesBefore, await CountAsync("licences"));
    }

    [SkippableFact]
    public async Task Post_with_second_item_failing_rolls_back_first_item()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var good = await CreateProductAsync("good", "Good", 10.00m, "USD");

        var ordersBefore = await CountAsync("orders");
        var licencesBefore = await CountAsync("licences");
        var auditBefore = await CountAsync("audit_events");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = good.Id, quantity = 1, labels = new string?[] { null } },
                new { productId = Guid.NewGuid(), quantity = 1, labels = new string?[] { null } }
            }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ordersBefore, await CountAsync("orders"));
        Assert.Equal(licencesBefore, await CountAsync("licences"));
        Assert.Equal(auditBefore, await CountAsync("audit_events"));
    }

    [SkippableFact]
    public async Task Post_writes_order_placed_and_licence_created_audit_events()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("audit-prod", "Audit Prod", 7.50m, "USD");

        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 2, labels = new string?[] { "a", "b" } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);

        var orderPlaced = await CountAsync("audit_events", "WHERE event_type = 'order.placed' AND subject_id = @id", new { id = body.Id });
        var licenceCreated = await CountAsync("audit_events", "WHERE event_type = 'licence.created' AND actor_type = 'user' AND actor_user_id = @uid", new { uid = AdminUserId });
        Assert.Equal(1, orderPlaced);
        Assert.True(licenceCreated >= 2);
    }

    [SkippableFact]
    public async Task Created_licence_from_order_verifies_via_verify_endpoint()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("verify-prod", "Verify Prod", 5.00m, "USD");

        var orderResp = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new string?[] { null } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, orderResp.StatusCode);
        var order = await orderResp.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(order);
        var key = order.Items[0].LicenceKey;

        using var anon = Factory!.CreateClient();
        var verify = await anon.PostAsJsonAsync("/licences/verify", new
        {
            licenceKey = key,
            productId = product.Id,
            clientNonce = GenerateClientNonce()
        });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [SkippableFact]
    public async Task Me_orders_returns_only_callers_orders()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("me-orders", "Me Orders", 5.00m, "USD");

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
        var product = await CreateProductAsync("not-mine", "Not Mine", 5.00m, "USD");
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
        var product = await CreateProductAsync("admin-list", "Admin List", 3.00m, "USD");
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
        var product = await CreateProductAsync("label-carry", "Label Carry", 3.00m, "USD");

        var resp = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId = product.Id, quantity = 1, labels = new[] { "Nickname" } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(created);

        var fetched = await AuthedClient.GetFromJsonAsync<OrderPayload>($"/me/orders/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Single(fetched.Items);
        Assert.Equal("Nickname", fetched.Items[0].Label);
        Assert.Null(fetched.Items[0].LicenceKey);
    }

    private async Task<OrderPayload> PlaceOrderAsync(HttpClient client, Guid productId)
    {
        var resp = await client.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId, quantity = 1, labels = new string?[] { null } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<int> CountAsync(string table, string? where = null, object? parameters = null)
    {
        await using var conn = await OpenDbAsync();
        var sql = $"SELECT COUNT(*) FROM {table} {where}";
        return (int)await Dapper.SqlMapper.ExecuteScalarAsync<long>(conn, sql, parameters);
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name, decimal? price = null, string? currency = null, bool isPublic = true)
    {
        object payload = price is null
            ? new { slug, displayName = name, isPublic }
            : new { slug, displayName = name, price, currency = currency ?? "USD", isPublic };
        var response = await AuthedClient.PostAsJsonAsync("/products", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

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
