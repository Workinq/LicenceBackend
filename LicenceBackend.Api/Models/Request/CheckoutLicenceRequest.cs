namespace LicenceBackend.Api.Models.Request;

public sealed record CheckoutLicenceRequest(
    string? LicenceKey,
    Guid? ProductId,
    string? ClientNonce,
    string? InstanceId,
    string? Hwid
);
