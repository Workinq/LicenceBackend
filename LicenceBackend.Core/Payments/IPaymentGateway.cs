namespace LicenceBackend.Core.Payments;

public sealed record PaymentIntentCreation(string PaymentIntentId, string ClientSecret);

public sealed record PaymentGatewayEvent(string Type, string? PaymentIntentId);

public interface IPaymentGateway
{
    Task<PaymentIntentCreation> CreatePaymentIntentAsync(
        long amountMinorUnits,
        string currency,
        CancellationToken cancellationToken);

    // Returns null when signature verification fails.
    PaymentGatewayEvent? ConstructEvent(string payload, string signatureHeader);
}
