namespace LicenceBackend.Core.Orders;

public sealed record OrderItem(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    Guid LicenceId,
    decimal? UnitPrice,
    string Currency,
    DateTimeOffset CreatedAt
);
