namespace LicenceBackend.Api.Models.Response;

public sealed record CheckoutSessionResponse(
    string? ClientSecret,
    Guid? CheckoutAttemptId,
    Guid? OrderId,
    bool Free);
