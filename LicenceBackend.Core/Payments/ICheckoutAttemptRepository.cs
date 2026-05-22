using System.Data;

namespace LicenceBackend.Core.Payments;

public interface ICheckoutAttemptRepository
{
    Task CreateAsync(
        CheckoutAttempt attempt,
        IReadOnlyList<CheckoutAttemptItem> items,
        CancellationToken cancellationToken);

    Task<CheckoutAttempt?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Guid?> FindIdByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken);

    Task<(CheckoutAttempt Attempt, IReadOnlyList<CheckoutAttemptItem> Items)?> LockByIdInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid id,
        CancellationToken cancellationToken);

    Task MarkFulfilledInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid attemptId,
        Guid orderId,
        DateTimeOffset fulfilledAt,
        CancellationToken cancellationToken);

    Task MarkFailedByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken);
}
