namespace LicenceBackend.Api.Models.Response;

public record OrderResponse(
    Guid Id,
    Guid UserId,
    string ContactEmail,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CurrencyTotalResponse> Totals,
    IReadOnlyList<OrderItemResponse> Items
);

public sealed record CurrencyTotalResponse(string Currency, decimal Amount);

public record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductSlug,
    string ProductDisplayName,
    Guid LicenceId,
    string? Label,
    decimal? UnitPrice,
    string Currency
);
