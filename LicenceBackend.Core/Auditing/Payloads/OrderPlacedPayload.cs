using LicenceBackend.Core.Orders;

namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record OrderPlacedPayload(
    int ItemCount,
    IReadOnlyList<CurrencyTotal> Totals,
    string ContactEmail
);
