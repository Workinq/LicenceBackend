namespace LicenceBackend.Api.Models.Response;

public sealed record LicenceKeyMintedResponse(
    LicenceKeyResponse Key,
    string LicenceKey
);
