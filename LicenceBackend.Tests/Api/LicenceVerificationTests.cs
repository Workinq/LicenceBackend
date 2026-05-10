using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceVerificationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Verify_without_hwid_on_unbound_licence_does_not_pin()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("verify-no-pin");

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn     = await OpenDbAsync();
        var             hwidHmac         = await conn.ExecuteScalarAsync<byte[]?>("SELECT hwid_hmac FROM licences WHERE id = @Id", new { Id = licenceId });
        Assert.Null(hwidHmac);
    }

    [SkippableFact]
    public async Task Valid_licence_returns_signed_payload_with_claims_and_nonce()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, productSlug, licenceId, licenceKey) = await CreateProductAndLicenceAsync("verify-ok");
        var clientNonce = GenerateClientNonce();

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignedPayloadResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.SignedPayload));
        Assert.Equal(2, body.SignedPayload.Count(c => c == '.')); // JWS compact has two dots

        var jwt = await VerifySignedLicencePayloadAsync(body.SignedPayload);

        Assert.Equal("licence-verify-test", jwt.Header["kid"]);
        Assert.Equal("ES256",               jwt.Header["alg"]);
        Assert.Equal("licence-verify+jwt",  jwt.Header["typ"]);

        Assert.Equal(clientNonce,          jwt.Payload["nonce"]);
        Assert.Equal(licenceId.ToString(), jwt.Payload["licenceId"]);
        Assert.Equal(productId.ToString(), jwt.Payload["productId"]);
        Assert.Equal(productSlug,          jwt.Payload["productSlug"]);
        Assert.Equal("active",             jwt.Payload["status"]);

        var iat = (long)jwt.Payload["iat"];
        var exp = (long)jwt.Payload["exp"];
        Assert.InRange(exp - iat, 55, 65);
    }

    [SkippableFact]
    public async Task Wrong_product_id_returns_invalid_licence_unsigned()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, _, licenceKey) = await CreateProductAndLicenceAsync("verify-wrong-product");

        using var client = Factory!.CreateClient();
        var response     = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_licence", body);
        Assert.DoesNotContain("signedPayload", body, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Unknown_licence_key_returns_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, _) = await CreateProductAndLicenceAsync("verify-unknown-key");

        using var client = Factory!.CreateClient();
        var response     = await client.PostAsJsonAsync("/licences/verify", new { licenceKey = "LIC-ABCDE-FGHJK-MNPQR-STVWX-YZ234", productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Missing_licence_key_returns_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client = Factory!.CreateClient();
        var response     = await client.PostAsJsonAsync("/licences/verify", new { licenceKey = "", productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Missing_client_nonce_returns_vague_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("verify-missing-nonce");

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_licence", body);
        Assert.DoesNotContain("ClientNonce", body, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Too_short_client_nonce_returns_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("verify-short-nonce");

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_licence", body);
    }

    [SkippableFact]
    public async Task Too_long_client_nonce_returns_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("verify-long-nonce");

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = new string('a', 200) });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Tampered_signed_payload_fails_signature_verification()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("verify-tamper");
        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body  = await response.Content.ReadFromJsonAsync<SignedPayloadResponse>();
        var parts = body!.SignedPayload.Split('.');
        Assert.Equal(3, parts.Length);

        // Flip a character in the signature segment so base64url still decodes cleanly
        // but the signature bytes no longer match the payload.
        var sig         = parts[2];
        var flipped      = sig[0] == 'A' ? 'B' : 'A';
        var tamperedSig = flipped + sig[1..];
        var tampered    = $"{parts[0]}.{parts[1]}.{tamperedSig}";

        await Assert.ThrowsAsync<SecurityTokenInvalidSignatureException>(async () => await VerifySignedLicencePayloadAsync(tampered));
    }

    [SkippableFact]
    public async Task Public_key_endpoint_returns_jwks_with_active_key()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client   = Factory!.CreateClient();
        var       response = await client.GetAsync("/licences/verify/public-key");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JwksPayload>();
        Assert.NotNull(body);
        Assert.Single(body.Keys);
        var jwk = body.Keys[0];
        Assert.Equal("EC",                  jwk.Kty);
        Assert.Equal("P-256",               jwk.Crv);
        Assert.Equal("licence-verify-test", jwk.Kid);
        Assert.Equal("ES256",               jwk.Alg);
        Assert.Equal("sig",                 jwk.Use);
        Assert.False(string.IsNullOrWhiteSpace(jwk.X));
        Assert.False(string.IsNullOrWhiteSpace(jwk.Y));
    }

    private async Task<(Guid productId, string productSlug, Guid licenceId, string licenceKey)>
        CreateProductAndLicenceAsync(string slug)
    {
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (product.Id, product.Slug, licence.Id, licence.LicenceKey);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record SignedPayloadResponse(string SignedPayload);
}
