using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace LicenceBackend.Tests.Api;

public sealed class InvoicesEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Placing_an_order_creates_an_invoice_row()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("invoice-product", "Invoice Product", 12.50m, "USD");

        var order = await PlaceOrderAsync(product.Id);

        await using var conn = await OpenDbAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM invoices WHERE order_id = @OrderId", new { OrderId = order.Id });
        Assert.Equal(1, count);

        var itemCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM invoice_line_items li JOIN invoices i ON i.id = li.invoice_id WHERE i.order_id = @OrderId",
            new { OrderId = order.Id });
        Assert.Equal(1, itemCount);
    }

    private async Task<ProductRef> CreateProductAsync(string slug, string name, decimal price, string currency)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new
        {
            slug,
            displayName = name,
            price,
            currency,
            isPublic = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductRef>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<OrderRef> PlaceOrderAsync(Guid productId)
    {
        var response = await AuthedClient.PostAsJsonAsync("/orders", new
        {
            items = new object[]
            {
                new { productId, quantity = 1, labels = new string?[] { null } }
            }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderRef>();
        Assert.NotNull(body);
        return body;
    }

    public sealed record OrderRef(Guid Id);

    public sealed record ProductRef(Guid Id, string Slug);
}
