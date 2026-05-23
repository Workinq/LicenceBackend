namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceKeysResponse(
    int ActiveCount,
    int ActiveCap,
    IReadOnlyList<LicenceKeyResponse> Keys
);
