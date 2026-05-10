namespace LicenceBackend.Api.Models.Request;

public sealed record VerifyLicenceRequest(
    string? LicenceKey,
    Guid? ProductId,
    string? ClientNonce,
    string? Hwid
);
