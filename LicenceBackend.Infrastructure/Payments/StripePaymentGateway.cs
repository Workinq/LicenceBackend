using LicenceBackend.Core.Payments;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Stripe;

namespace LicenceBackend.Infrastructure.Payments;

public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly string _webhookSecret;
    private readonly PaymentIntentService _paymentIntents;

    public StripePaymentGateway(IOptions<StripeOptions> options)
    {
        var opts = options.Value;
        _webhookSecret = opts.WebhookSigningSecret;
        _paymentIntents = new PaymentIntentService(new StripeClient(opts.SecretKey));
    }

    public async Task<PaymentIntentCreation> CreatePaymentIntentAsync(
        long amountMinorUnits, string currency, CancellationToken cancellationToken)
    {
        var intent = await _paymentIntents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = amountMinorUnits,
                Currency = currency.ToLowerInvariant(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true }
            },
            cancellationToken: cancellationToken);
        return new PaymentIntentCreation(intent.Id, intent.ClientSecret);
    }

    public PaymentGatewayEvent? ConstructEvent(string payload, string signatureHeader)
    {
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(
                payload, signatureHeader, _webhookSecret, throwOnApiVersionMismatch: false);
            var paymentIntentId = (stripeEvent.Data.Object as PaymentIntent)?.Id;
            return new PaymentGatewayEvent(stripeEvent.Type, paymentIntentId);
        }
        catch (StripeException)
        {
            return null;
        }
    }
}
