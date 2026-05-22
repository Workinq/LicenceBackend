namespace LicenceBackend.Core.Payments;

public sealed record CheckoutAttempt(
    Guid Id,
    Guid UserId,
    string ContactEmail,
    string Currency,
    decimal AmountTotal,
    string StripePaymentIntentId,
    CheckoutAttemptStatus Status,
    Guid? OrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FulfilledAt);
