using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using LicenceBackend.Core.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace LicenceBackend.Tests.Api;

public sealed class SessionsEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Login_with_valid_credentials_returns_access_token_and_user()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(AdminEmail, body.User.Email);
        Assert.Equal("admin", body.User.Role);
    }

    [SkippableFact]
    public async Task Login_with_wrong_password_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Login_with_unknown_email_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = "nobody@nowhere.invalid", password = "doesn't-matter-12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Login_unknown_email_runs_password_verify_to_equalise_timing()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var verifyCalls = 0;
        var dummyCalls = 0;
        var counter = new CountingPasswordHasher(() => Interlocked.Increment(ref verifyCalls), () => Interlocked.Increment(ref dummyCalls));

        await using var customFactory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPasswordHasher>();
                    services.AddSingleton<IPasswordHasher>(counter);
                });
            });

        try
        {
            using var client = customFactory.CreateClient();
            var response = await client.PostAsJsonAsync("/sessions", new { email = "no-such-user@example.invalid", password = "anything-non-empty" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, verifyCalls);
            Assert.Equal(1, dummyCalls);
        }
        finally
        {
            var dataSource = customFactory.Services.GetService<NpgsqlDataSource>();
            if (dataSource is not null) await dataSource.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Login_email_is_case_insensitive()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail.ToUpperInvariant(), password = AdminPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_without_auth_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.DeleteAsync("/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Logout_with_valid_jwt_returns_204()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.DeleteAsync("/sessions");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [SkippableFact]
    public async Task Token_without_exp_claim_is_rejected()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var pemPath = Path.Combine(TempDir, "session-signing.pem");
        var pem = await File.ReadAllTextAsync(pemPath);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        var key = new ECDsaSecurityKey(ecdsa) { KeyId = "session-v1" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var jwt = new JwtSecurityToken(
            "https://licencebackend.test",
            "licencebackend-dashboard",
            [
                new Claim("sub",  AdminUserId.ToString()),
                new Claim("role", "admin"),
                new Claim("sid",  Guid.NewGuid().ToString())
            ],
            null,
            null,
            creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Access_token_from_login_is_accepted_on_admin_endpoint()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var login = await UnauthedClient.PostAsJsonAsync("/sessions", new { email = AdminEmail, password = AdminPassword });
        var session = await login.Content.ReadFromJsonAsync<SessionPayload>();
        Assert.NotNull(session);

        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record SessionPayload(
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAt,
        UserPayload User
    );

    private sealed record UserPayload(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);

    private sealed class CountingPasswordHasher(Action onVerify, Action onDummy) : IPasswordHasher
    {
        public string Hash(string password)
        {
            return password;
        }

        public bool Verify(string password, string encodedHash)
        {
            onVerify();
            return false;
        }

        public void VerifyDummy(string password)
        {
            onDummy();
        }
    }
}
