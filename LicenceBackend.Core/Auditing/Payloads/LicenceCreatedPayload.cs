namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCreatedPayload(
    Guid OrderId,
    Guid ProductId,
    decimal? UnitPrice,
    string Currency,
    string? Label
);
