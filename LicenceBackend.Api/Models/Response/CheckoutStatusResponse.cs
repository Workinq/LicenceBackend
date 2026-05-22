namespace LicenceBackend.Api.Models.Response;

public sealed record CheckoutStatusResponse(string Status, Guid? OrderId);
