using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace LicenceBackend.Tests.Api;

public sealed class SigningKeyRotationTests : IntegrationTestBase
{
    private const string Issuer   = "https://licencebackend.test";
    private const string Audience = "licencebackend-dashboard";

    private string _sessionV1Kid     = string.Empty;
    private string _sessionV1PemPath = string.Empty;
    private string _sessionV2Kid     = string.Empty;
    private string _sessionV2PemPath = string.Empty;
    private string _verifyV1Kid      = string.Empty;
    private string _verifyV1PemPath  = string.Empty;
    private string _verifyV2Kid      = string.Empty;
    private string _verifyV2PemPath  = string.Empty;

    protected override void ApplyPreFactoryEnvironment()
    {
        var suffix = Guid.NewGuid().ToString("N");
        _sessionV1Kid = $"session-rot-v1-{suffix}";
        _sessionV2Kid = $"session-rot-v2-{suffix}";
        _verifyV1Kid  = $"verify-rot-v1-{suffix}";
        _verifyV2Kid  = $"verify-rot-v2-{suffix}";

        _sessionV1PemPath = Path.Combine(TempDir, $"session-{suffix}-v1.pem");
        _sessionV2PemPath = Path.Combine(TempDir, $"session-{suffix}-v2.pem");
        _verifyV1PemPath  = Path.Combine(TempDir, $"verify-{suffix}-v1.pem");
        _verifyV2PemPath  = Path.Combine(TempDir, $"verify-{suffix}-v2.pem");
        WriteFreshEcdsaPem(_sessionV1PemPath);
        WriteFreshEcdsaPem(_sessionV2PemPath);
        WriteFreshEcdsaPem(_verifyV1PemPath);
        WriteFreshEcdsaPem(_verifyV2PemPath);

        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__Kid",            _sessionV1Kid);
        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath", _sessionV1PemPath);
        Environment.SetEnvironmentVariable("SessionSigning__Keys__1__Kid",            _sessionV2Kid);
        Environment.SetEnvironmentVariable("SessionSigning__Keys__1__PrivateKeyPath", _sessionV2PemPath);
        Environment.SetEnvironmentVariable("SessionSigning__ActiveKid",               _sessionV2Kid);

        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__Kid",            _verifyV1Kid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__PrivateKeyPath", _verifyV1PemPath);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__1__Kid",            _verifyV2Kid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__1__PrivateKeyPath", _verifyV2PemPath);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__ActiveKid",               _verifyV2Kid);
    }

    [SkippableFact]
    public async Task Token_signed_under_old_kid_still_authorizes_after_rotation()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var legacyToken = MintSessionJwt(_sessionV1PemPath, _sessionV1Kid, AdminUserId);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", legacyToken);
        var response = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Token_signed_under_unknown_kid_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var rogueKidPath = Path.Combine(TempDir, $"rogue-{Guid.NewGuid():N}.pem");
        WriteFreshEcdsaPem(rogueKidPath);
        var rogueToken = MintSessionJwt(rogueKidPath, "rogue-kid-" + Guid.NewGuid().ToString("N"), AdminUserId);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rogueToken);
        var response = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Token_signed_under_kid_removed_from_config_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var legacyToken = MintSessionJwt(_sessionV1PemPath, _sessionV1Kid, AdminUserId);

        var savedKid0  = Environment.GetEnvironmentVariable("SessionSigning__Keys__0__Kid");
        var savedPath0 = Environment.GetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath");
        var savedKid1  = Environment.GetEnvironmentVariable("SessionSigning__Keys__1__Kid");
        var savedPath1 = Environment.GetEnvironmentVariable("SessionSigning__Keys__1__PrivateKeyPath");
        try
        {
            Environment.SetEnvironmentVariable("SessionSigning__Keys__0__Kid",            _sessionV2Kid);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath", _sessionV2PemPath);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__1__Kid",            null);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__1__PrivateKeyPath", null);

            await using var trimmedFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

            using var client = trimmedFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", legacyToken);
            var response = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var dataSource = trimmedFactory.Services.GetService<NpgsqlDataSource>();
            if (dataSource is not null) await dataSource.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SessionSigning__Keys__0__Kid",            savedKid0);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath", savedPath0);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__1__Kid",            savedKid1);
            Environment.SetEnvironmentVariable("SessionSigning__Keys__1__PrivateKeyPath", savedPath1);
        }
    }

    [SkippableFact]
    public async Task Fresh_login_returns_access_token_signed_under_active_kid()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(session);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();
        var jwt = handler.ReadJwtToken(session.AccessToken);
        Assert.Equal(_sessionV2Kid, jwt.Header.Kid);
    }

    [SkippableFact]
    public async Task Jwks_endpoint_returns_all_loaded_licence_verify_keys()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var jwks = await FetchJwksAsync();
        var kids = jwks.Keys.Select(k => k.Kid).ToHashSet();
        Assert.Contains(_verifyV1Kid, kids);
        Assert.Contains(_verifyV2Kid, kids);
        Assert.All(jwks.Keys, k => Assert.Equal("ES256", k.Alg));
        Assert.All(jwks.Keys, k => Assert.Equal("EC",    k.Kty));
        Assert.All(jwks.Keys, k => Assert.Equal("P-256", k.Crv));
    }

    [SkippableFact]
    public async Task Signed_verify_response_carries_active_kid_in_header()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await CreateProductAndLicenceAsync();

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body    = await response.Content.ReadFromJsonAsync<SignedPayloadResponse>();
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();
        var jwt = handler.ReadJwtToken(body!.SignedPayload);
        Assert.Equal(_verifyV2Kid, jwt.Header.Kid);
    }

    [SkippableFact]
    public async Task Verify_signature_chain_resolves_via_jwks_kid_lookup()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await CreateProductAndLicenceAsync();

        using var client   = Factory!.CreateClient();
        var       response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SignedPayloadResponse>();

        var token = await VerifySignedLicencePayloadAsync(body!.SignedPayload);
        Assert.Equal(_verifyV2Kid, token.Header.Kid);
    }

    private static void WriteFreshEcdsaPem(string path)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(path, ecdsa.ExportPkcs8PrivateKeyPem());
    }

    private static string MintSessionJwt(string pemPath, string kid, Guid userId)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(File.ReadAllText(pemPath));
        var key   = new ECDsaSecurityKey(ecdsa) { KeyId = kid };
        var creds = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var now = DateTimeOffset.UtcNow;
        var jwt = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, AdminEmail),
                new Claim("role",                        "admin"),
                new Claim("sid",                         Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            ],
            now.UtcDateTime,
            now.AddMinutes(15).UtcDateTime,
            creds);

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();
        return handler.WriteToken(jwt);
    }

    private async Task<(Guid productId, string licenceKey)> CreateProductAndLicenceAsync()
    {
        var slug            = "rot-product-" + Guid.NewGuid().ToString("N")[..8];
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product!.Id, userId = AdminUserId });
        licenceResponse.EnsureSuccessStatusCode();
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        return (product.Id, licence!.LicenceKey);
    }

    private sealed record SessionPayload(
        string         AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        string         RefreshToken,
        DateTimeOffset RefreshTokenExpiresAt
    );

    private sealed record SignedPayloadResponse(string SignedPayload);

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName);

    private sealed record LicenceCreatedPayload(Guid Id, string LicenceKey);
}
