namespace LicenceBackend.Api.Models.Response;

public sealed record JwksResponse(IReadOnlyList<JwkEntry> Keys);

public sealed record JwkEntry(
    string Kty,
    string Crv,
    string X,
    string Y,
    string Kid,
    string Alg,
    string Use
);
