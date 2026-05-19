using System.ComponentModel.DataAnnotations;

namespace LicenceBackend.Api.Models.Request;

public sealed record CreateOrderRequest(
    string? ContactEmail,
    IReadOnlyList<CreateOrderItemRequest> Items
);

public sealed record CreateOrderItemRequest(
    [Required] Guid ProductId,
    [Required] int Quantity,
    IReadOnlyList<string?>? Labels
);
