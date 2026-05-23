using LicenceBackend.Infrastructure.Options;
using LicenceBackend.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Stripe;

namespace LicenceBackend.Tests.Unit.Payments;

public sealed class StripePaymentGatewayTests
{
    private const string WebhookSecret = "whsec_test_secret_for_unit_tests";

    private static StripePaymentGateway CreateGateway()
    {
        var options = Options.Create(new StripeOptions
        {
            SecretKey = "sk_test_dummy",
            PublishableKey = "pk_test_dummy",
            WebhookSigningSecret = WebhookSecret
        });
        return new StripePaymentGateway(options);
    }

    private static string SignedHeader(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = EventUtility.ComputeSignature(secret, timestamp, payload);
        return $"t={timestamp},v1={signature}";
    }

    [Fact]
    public void ConstructEvent_returns_null_when_signature_header_is_invalid()
    {
        var gateway = CreateGateway();
        var payload = "{\"id\":\"evt_1\",\"object\":\"event\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_abc\",\"object\":\"payment_intent\"}}}";

        var result = gateway.ConstructEvent(payload, "t=0,v1=not-a-real-signature");

        Assert.Null(result);
    }

    [Fact]
    public void ConstructEvent_returns_null_when_signed_with_wrong_secret()
    {
        var gateway = CreateGateway();
        var payload = "{\"id\":\"evt_1\",\"object\":\"event\",\"api_version\":\"2020-08-27\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_abc\",\"object\":\"payment_intent\"}}}";
        var header = SignedHeader(payload, "whsec_some_other_secret");

        var result = gateway.ConstructEvent(payload, header);

        Assert.Null(result);
    }

    [Fact]
    public void ConstructEvent_returns_event_with_payment_intent_id_for_payment_intent_payload()
    {
        var gateway = CreateGateway();
        var payload = "{\"id\":\"evt_1\",\"object\":\"event\",\"api_version\":\"2020-08-27\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_abc\",\"object\":\"payment_intent\"}}}";
        var header = SignedHeader(payload, WebhookSecret);

        var result = gateway.ConstructEvent(payload, header);

        Assert.NotNull(result);
        Assert.Equal("payment_intent.succeeded", result!.Type);
        Assert.Equal("pi_abc", result.PaymentIntentId);
    }

    [Fact]
    public void ConstructEvent_returns_event_with_null_payment_intent_id_for_non_payment_intent_payload()
    {
        var gateway = CreateGateway();
        var payload = "{\"id\":\"evt_2\",\"object\":\"event\",\"api_version\":\"2020-08-27\",\"type\":\"customer.created\",\"data\":{\"object\":{\"id\":\"cus_xyz\",\"object\":\"customer\"}}}";
        var header = SignedHeader(payload, WebhookSecret);

        var result = gateway.ConstructEvent(payload, header);

        Assert.NotNull(result);
        Assert.Equal("customer.created", result!.Type);
        Assert.Null(result.PaymentIntentId);
    }

    [Fact]
    public void ConstructEvent_does_not_throw_on_api_version_mismatch()
    {
        var gateway = CreateGateway();
        var payload = "{\"id\":\"evt_3\",\"object\":\"event\",\"api_version\":\"1900-01-01\",\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":\"pi_xyz\",\"object\":\"payment_intent\"}}}";
        var header = SignedHeader(payload, WebhookSecret);

        var result = gateway.ConstructEvent(payload, header);

        Assert.NotNull(result);
        Assert.Equal("pi_xyz", result!.PaymentIntentId);
    }
}
