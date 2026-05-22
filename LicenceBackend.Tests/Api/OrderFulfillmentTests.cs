using Dapper;
using LicenceBackend.Core.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace LicenceBackend.Tests.Api;

public sealed class OrderFulfillmentTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Fulfill_mints_order_and_keyless_licences_and_is_idempotent()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, price, currency) VALUES (@Id, @Slug, 'Fulfil Test', 9.99, 'USD');",
                new { Id = productId, Slug = $"fulfil-{productId:N}" });
            await conn.ExecuteAsync(
                """
                INSERT INTO checkout_attempts (id, user_id, contact_email, currency, amount_total, stripe_payment_intent_id, status)
                VALUES (@Id, @UserId, 'buyer@test.local', 'USD', 19.98, @Pi, 'pending');
                """,
                new { Id = attemptId, UserId = AdminUserId, Pi = $"pi_{attemptId:N}" });
            await conn.ExecuteAsync(
                """
                INSERT INTO checkout_attempt_items (id, checkout_attempt_id, product_id, quantity, labels, unit_price, currency)
                VALUES (@Id, @AttemptId, @ProductId, 2, '[null,null]'::jsonb, 9.99, 'USD');
                """,
                new { Id = Guid.NewGuid(), AttemptId = attemptId, ProductId = productId });
        }

        var fulfillment = Factory!.Services.GetRequiredService<IOrderFulfillmentService>();

        var orderId1 = await fulfillment.FulfillAsync(attemptId, CancellationToken.None);
        var orderId2 = await fulfillment.FulfillAsync(attemptId, CancellationToken.None);

        Assert.Equal(orderId1, orderId2);

        await using (var conn = await OpenDbAsync())
        {
            var orderCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM orders WHERE id = @Id;", new { Id = orderId1 });
            Assert.Equal(1, orderCount);

            var licenceCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM order_items WHERE order_id = @Id;", new { Id = orderId1 });
            Assert.Equal(2, licenceCount);

            var keyless = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*) FROM licences l
                JOIN order_items oi ON oi.licence_id = l.id
                WHERE oi.order_id = @Id AND l.key_hmac IS NULL;
                """,
                new { Id = orderId1 });
            Assert.Equal(2, keyless);

            var invoiceCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM invoices WHERE order_id = @Id;", new { Id = orderId1 });
            Assert.Equal(1, invoiceCount);
        }
    }
}
