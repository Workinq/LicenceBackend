using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Infrastructure.Crypto;
using LicenceBackend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace LicenceBackend.Tests.Api;

[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private const string TestConnEnv = "LICENCEBACKEND_TEST_POSTGRES";
    private const string Issuer = "https://licencebackend.test";
    private const string Audience = "licencebackend-dashboard";
    private const string SessionKid = "session-v1";
    private const string LicenceVerifyKid = "licence-verify-test";
    protected const string AdminEmail = "admin@test.local";
    protected const string AdminPassword = "admin-integration-test-pw!";

    protected static readonly Uri HttpsBaseAddress = new("https://localhost");

    private string? _connectionString;
    protected Guid AdminUserId;
    protected HttpClient AuthedClient = null!;
    protected WebApplicationFactory<Program>? Factory;
    protected HttpClient UnauthedClient = null!;
    protected string TempDir { get; private set; } = string.Empty;

    public virtual async Task InitializeAsync()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(TestConnEnv);
        Skip.If(string.IsNullOrWhiteSpace(rawConnectionString), $"Set {TestConnEnv} to a Postgres connection string to run integration tests.");
        _connectionString = EnsureBoundedPool(rawConnectionString);

        TempDir = Directory.CreateTempSubdirectory("licencebackend-tests-").FullName;
        var sessionPemPath = Path.Combine(TempDir, "session-signing.pem");
        var licenceVerifyPemPath = Path.Combine(TempDir, "licence-verify-signing.pem");
        var pepperPath = Path.Combine(TempDir, "pepper.txt");

        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            await File.WriteAllTextAsync(sessionPemPath, ecdsa.ExportPkcs8PrivateKeyPem());
        }

        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            await File.WriteAllTextAsync(licenceVerifyPemPath, ecdsa.ExportPkcs8PrivateKeyPem());
        }

        var pepper = RandomNumberGenerator.GetBytes(32);
        await File.WriteAllTextAsync(pepperPath, Convert.ToBase64String(pepper));

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
        }

        var migrationsDir = FindRepoDirectory("migrations");
        var migrationResult = SchemaMigrator.Run(_connectionString, migrationsDir, NullLogger.Instance);
        if (!migrationResult.Successful)
            throw new InvalidOperationException(
                $"Migration failed in script '{migrationResult.ErrorScript?.Name}': {migrationResult.Error}",
                migrationResult.Error);

        var hasher = new Argon2IdPasswordHasher();
        var adminHash = hasher.Hash(AdminPassword);
        AdminUserId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                """
                INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
                VALUES (@Id, @Email, @EmailLower, @Hash, NULL, 'admin', 'active', NOW(), NOW());
                """,
                new
                {
                    Id = AdminUserId,
                    Email = AdminEmail,
                    EmailLower = AdminEmail.ToLowerInvariant(),
                    Hash = adminHash
                });
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", _connectionString);
        ClearHigherIndices("SessionSigning__Keys");
        ClearHigherIndices("LicenceVerifySigning__Keys");
        ClearHigherIndices("Licence__Peppers");
        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__Kid", SessionKid);
        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath", sessionPemPath);
        Environment.SetEnvironmentVariable("SessionSigning__ActiveKid", SessionKid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__Kid", LicenceVerifyKid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__PrivateKeyPath", licenceVerifyPemPath);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__ActiveKid", LicenceVerifyKid);
        Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", "1");
        Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", pepperPath);
        Environment.SetEnvironmentVariable("Licence__ActivePepperVersion", "1");
        Environment.SetEnvironmentVariable("Session__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Session__Audience", Audience);
        Environment.SetEnvironmentVariable("Session__TtlSeconds", "900");
        Environment.SetEnvironmentVariable("Session__RefreshTtlSeconds", "2592000");
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");

        ApplyPreFactoryEnvironment();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        UnauthedClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = HttpsBaseAddress,
            HandleCookies = true
        });
        AuthedClient = await CreateLoggedInClientAsync(AdminEmail, AdminPassword);
    }

    public virtual async Task DisposeAsync()
    {
        UnauthedClient.Dispose();
        AuthedClient.Dispose();

        if (Factory is not null)
        {
            var dataSource = Factory.Services.GetService<NpgsqlDataSource>();
            if (dataSource is not null) await dataSource.DisposeAsync();
            await Factory.DisposeAsync();
        }

        if (!string.IsNullOrWhiteSpace(_connectionString))
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await conn.ExecuteAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
            }
            catch
            {
                // ignored
            }

        if (!string.IsNullOrEmpty(TempDir) && Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
    }

    protected virtual void ApplyPreFactoryEnvironment()
    {
    }

    private static void ClearHigherIndices(string prefix)
    {
        for (var i = 1; i <= 7; i++)
        {
            foreach (var leaf in new[] { "Kid", "PrivateKeyPath", "Version", "Path" })
            {
                Environment.SetEnvironmentVariable($"{prefix}__{i}__{leaf}", null);
            }
        }
    }

    protected HttpClient ClientFromIp(string ip)
    {
        var client = Factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = HttpsBaseAddress,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    protected async Task<HttpClient> AuthedClientFromIpAsync(string email, string password, string ip)
    {
        var client = await CreateLoggedInClientAsync(email, password);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    protected async Task<NpgsqlConnection> OpenDbAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    internal string ConnectionString => _connectionString!;

    protected async Task<HttpClient> CreateLoggedInClientAsync(string email, string password)
    {
        var loginClient = Factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = HttpsBaseAddress,
            HandleCookies = true
        });
        var response = await loginClient.PostAsJsonAsync("/sessions", new { email, password });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to log in as '{email}': {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<SessionPayload>() ?? throw new InvalidOperationException("Empty session response payload.");
        loginClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        return loginClient;
    }

    protected static string GenerateClientNonce()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }

    protected async Task<JwtSecurityToken> VerifySignedLicencePayloadAsync(string signedPayload)
    {
        var jwks = await FetchJwksAsync();
        var keys = jwks.Keys.Select(k => (SecurityKey)new JsonWebKey
        {
            Kty = k.Kty,
            Crv = k.Crv,
            X = k.X,
            Y = k.Y,
            Kid = k.Kid,
            Alg = k.Alg,
            Use = k.Use
        }).ToArray();

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        handler.ValidateToken(signedPayload, validationParameters, out var validatedToken);
        return (JwtSecurityToken)validatedToken;
    }

    protected async Task<JwksPayload> FetchJwksAsync()
    {
        var response = await UnauthedClient.GetAsync("/licences/verify/public-key");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JwksPayload>() ?? throw new InvalidOperationException("Empty JWKS response.");
    }

    private static string EnsureBoundedPool(string connectionString)
    {
        if (connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase)) return connectionString;
        var trimmed = connectionString.TrimEnd(';');
        return $"{trimmed};Maximum Pool Size=3";
    }

    internal static string FindRepoDirectory(string name)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LicenceBackend.sln")))
            {
                var candidate = Path.Combine(dir.FullName, name);
                if (Directory.Exists(candidate)) return candidate;
                throw new DirectoryNotFoundException($"Repo root '{dir.FullName}' does not contain '{name}'.");
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repo root (LicenceBackend.sln) by walking up from '{Directory.GetCurrentDirectory()}'.");
    }

    private sealed record SessionPayload(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt
    );

    public sealed record JwksPayload(IReadOnlyList<JwkPayload> Keys);

    public sealed record JwkPayload(
        string Kty,
        string Crv,
        string X,
        string Y,
        string Kid,
        string Alg,
        string Use
    );
}
