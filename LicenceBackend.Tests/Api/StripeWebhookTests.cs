using System.Net;
using System.Text;
using Dapper;
using LicenceBackend.Core.Payments;

namespace LicenceBackend.Tests.Api;

public sealed class StripeWebhookTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Invalid_signature_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        FakeGateway.NextEvent = null; // simulates ConstructEvent rejecting the signature

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "bad");
        var response = await UnauthedClient.PostAsync("/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Payment_succeeded_event_fulfills_the_attempt()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var paymentIntentId = $"pi_hook_{attemptId:N}";
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, price, currency) VALUES (@Id, @Slug, 'Hook Test', 5.00, 'USD');",
                new { Id = productId, Slug = $"hook-{productId:N}" });
            await conn.ExecuteAsync(
                """
                INSERT INTO checkout_attempts (id, user_id, contact_email, currency, amount_total, stripe_payment_intent_id, status)
                VALUES (@Id, @UserId, 'buyer@test.local', 'USD', 5.00, @Pi, 'pending');
                """,
                new { Id = attemptId, UserId = AdminUserId, Pi = paymentIntentId });
            await conn.ExecuteAsync(
                """
                INSERT INTO checkout_attempt_items (id, checkout_attempt_id, product_id, quantity, labels, unit_price, currency)
                VALUES (@Id, @AttemptId, @ProductId, 1, '[null]'::jsonb, 5.00, 'USD');
                """,
                new { Id = Guid.NewGuid(), AttemptId = attemptId, ProductId = productId });
        }

        FakeGateway.NextEvent = new PaymentGatewayEvent("payment_intent.succeeded", paymentIntentId);

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        content.Headers.Add("Stripe-Signature", "valid");
        var response = await UnauthedClient.PostAsync("/stripe/webhook", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verify = await OpenDbAsync();
        var status = await verify.ExecuteScalarAsync<string>(
            "SELECT status FROM checkout_attempts WHERE id = @Id;", new { Id = attemptId });
        Assert.Equal("fulfilled", status);
    }
}
