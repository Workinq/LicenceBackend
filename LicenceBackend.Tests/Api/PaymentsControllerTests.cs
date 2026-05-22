using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace LicenceBackend.Tests.Api;

public sealed class PaymentsControllerTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Checkout_creates_attempt_and_returns_client_secret()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, price, currency, is_public) VALUES (@Id, @Slug, 'Pay Test', 5.00, 'USD', true);",
                new { Id = productId, Slug = $"pay-{productId:N}" });
        }

        var response = await AuthedClient.PostAsJsonAsync("/payments/checkout", new
        {
            contactEmail = (string?)null,
            items = new[] { new { productId, quantity = 1, labels = new string?[] { null } } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckoutSessionPayload>();
        Assert.NotNull(body);
        Assert.False(body.Free);
        Assert.NotNull(body.ClientSecret);
        Assert.NotNull(body.CheckoutAttemptId);
    }

    [SkippableFact]
    public async Task Checkout_with_zero_total_fulfills_immediately()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, price, currency, is_public) VALUES (@Id, @Slug, 'Free Test', NULL, 'USD', true);",
                new { Id = productId, Slug = $"free-{productId:N}" });
        }

        var response = await AuthedClient.PostAsJsonAsync("/payments/checkout", new
        {
            contactEmail = (string?)null,
            items = new[] { new { productId, quantity = 1, labels = new string?[] { null } } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckoutSessionPayload>();
        Assert.NotNull(body);
        Assert.True(body.Free);
        Assert.NotNull(body.OrderId);
    }

    private sealed record CheckoutSessionPayload(
        string? ClientSecret, Guid? CheckoutAttemptId, Guid? OrderId, bool Free);
}
