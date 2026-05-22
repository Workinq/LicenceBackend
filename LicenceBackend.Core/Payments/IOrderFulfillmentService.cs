namespace LicenceBackend.Core.Payments;

public interface IOrderFulfillmentService
{
    // Idempotent. Returns the order id, creating it on first call.
    Task<Guid> FulfillAsync(Guid checkoutAttemptId, CancellationToken cancellationToken);
}
