using LicenceBackend.Core.Payments;

namespace LicenceBackend.Tests.Api;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public PaymentGatewayEvent? NextEvent { get; set; }

    public Task<PaymentIntentCreation> CreatePaymentIntentAsync(
        long amountMinorUnits, string currency, CancellationToken cancellationToken)
    {
        var id = $"pi_fake_{Guid.NewGuid():N}";
        return Task.FromResult(new PaymentIntentCreation(id, $"{id}_secret_fake"));
    }

    public PaymentGatewayEvent? ConstructEvent(string payload, string signatureHeader) => NextEvent;
}
