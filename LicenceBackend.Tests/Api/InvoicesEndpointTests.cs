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

    [SkippableFact]
    public async Task Buyer_can_read_their_own_invoice()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("inv-read", "Inv Read", 30.00m, "USD");
        var order = await PlaceOrderAsync(product.Id);

        var response = await AuthedClient.GetAsync($"/me/orders/{order.Id}/invoice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<InvoicePayload>();
        Assert.NotNull(invoice);
        Assert.Equal(order.Id, invoice.OrderId);
        Assert.StartsWith("INV-", invoice.InvoiceNumber);
        Assert.Single(invoice.LineItems);
        Assert.Equal("Inv Read", invoice.LineItems[0].ProductName);
        Assert.Equal(30.00m, invoice.Totals[0].Amount);
    }

    [SkippableFact]
    public async Task Buyer_cannot_read_another_users_invoice()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("inv-foreign", "Inv Foreign", 5.00m, "USD");
        var order = await PlaceOrderAsync(product.Id);

        var email = "invoice-foreign@test.local";
        var password = "invoice-foreign-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        using var regular = await CreateLoggedInClientAsync(email, password);

        var response = await regular.GetAsync($"/me/orders/{order.Id}/invoice");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_can_read_any_invoice()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("inv-admin", "Inv Admin", 7.00m, "USD");
        var order = await PlaceOrderAsync(product.Id);

        var response = await AuthedClient.GetAsync($"/admin/orders/{order.Id}/invoice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Invoice_numbers_are_sequential()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("inv-seq", "Inv Seq", 1.00m, "USD");
        var first = await PlaceOrderAsync(product.Id);
        var second = await PlaceOrderAsync(product.Id);

        var firstInvoice = await (await AuthedClient.GetAsync($"/me/orders/{first.Id}/invoice"))
            .Content.ReadFromJsonAsync<InvoicePayload>();
        var secondInvoice = await (await AuthedClient.GetAsync($"/me/orders/{second.Id}/invoice"))
            .Content.ReadFromJsonAsync<InvoicePayload>();

        var firstNumber = int.Parse(firstInvoice!.InvoiceNumber.Replace("INV-", ""));
        var secondNumber = int.Parse(secondInvoice!.InvoiceNumber.Replace("INV-", ""));
        Assert.Equal(firstNumber + 1, secondNumber);
    }

    [SkippableFact]
    public async Task Invoice_line_item_snapshot_survives_product_rename()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("inv-rename", "Original Name", 9.00m, "USD");
        var order = await PlaceOrderAsync(product.Id);

        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "UPDATE products SET display_name = 'Renamed' WHERE id = @Id", new { product.Id });
        }

        var invoice = await (await AuthedClient.GetAsync($"/me/orders/{order.Id}/invoice"))
            .Content.ReadFromJsonAsync<InvoicePayload>();
        Assert.Equal("Original Name", invoice!.LineItems[0].ProductName);
    }

    [SkippableFact]
    public async Task Unknown_order_invoice_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/me/orders/{Guid.NewGuid()}/invoice");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    public sealed record InvoicePayload(
        Guid OrderId,
        string InvoiceNumber,
        IReadOnlyList<InvoiceLinePayload> LineItems,
        IReadOnlyList<TotalPayload> Totals);

    public sealed record InvoiceLinePayload(string ProductName, decimal? UnitPrice, string Currency);

    public sealed record TotalPayload(string Currency, decimal Amount);
}
