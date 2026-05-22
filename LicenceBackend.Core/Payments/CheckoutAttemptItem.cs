namespace LicenceBackend.Core.Payments;

public sealed record CheckoutAttemptItem(
    Guid Id,
    Guid CheckoutAttemptId,
    Guid ProductId,
    int Quantity,
    IReadOnlyList<string?> Labels,
    decimal? UnitPrice,
    string Currency);
