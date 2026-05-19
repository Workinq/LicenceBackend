namespace LicenceBackend.Core.Orders;

public sealed record Order(
    Guid Id,
    Guid UserId,
    string ContactEmail,
    OrderStatus Status,
    DateTimeOffset CreatedAt
);
