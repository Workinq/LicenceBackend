using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Infrastructure.Crypto;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Tests.Api;

public sealed class RateLimitingTests : IntegrationTestBase
{
    private const int PermitLimit = 2;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Override the admin client to log in from a dedicated IP. This keeps the admin's (IP, email)
        // login bucket isolated from the IPs used by the per-test login rate-limit cases, and leaves the
        // admin's per-user "admin" bucket untouched (login uses the login bucket, not admin).
        AuthedClient.Dispose();
        AuthedClient = await CreateLoggedInAsIpAsync(AdminEmail, AdminPassword, "10.9.0.1");
    }

    protected override void ApplyPreFactoryEnvironment()
    {
        // Flip rate limiting on with tiny limits so a handful of requests exhaust each bucket.
        // Use fresh signing keys + unique kids so the Microsoft.IdentityModel signature-provider cache
        // (process-wide, keyed by kid) doesn't collide with other test classes' cached providers.
        var sessionPem = Path.Combine(TempDir, "rl-session-signing.pem");
        var verifyPem = Path.Combine(TempDir, "rl-licence-verify-signing.pem");
        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            File.WriteAllText(sessionPem, ecdsa.ExportPkcs8PrivateKeyPem());
        }

        using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
        {
            File.WriteAllText(verifyPem, ecdsa.ExportPkcs8PrivateKeyPem());
        }

        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "true");
        Environment.SetEnvironmentVariable("RateLimiting__Login__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Login__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Refresh__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Refresh__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Verify__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Verify__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__VerifyPublicKey__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__VerifyPublicKey__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Admin__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Admin__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Checkout__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Checkout__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Heartbeat__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Heartbeat__WindowSeconds", "60");
        Environment.SetEnvironmentVariable("RateLimiting__Checkin__PermitLimit", PermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__Checkin__WindowSeconds", "60");
        var sessionKid = "rl-session-" + Guid.NewGuid().ToString("N");
        var verifyKid = "rl-verify-" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__Kid", sessionKid);
        Environment.SetEnvironmentVariable("SessionSigning__Keys__0__PrivateKeyPath", sessionPem);
        Environment.SetEnvironmentVariable("SessionSigning__ActiveKid", sessionKid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__Kid", verifyKid);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__Keys__0__PrivateKeyPath", verifyPem);
        Environment.SetEnvironmentVariable("LicenceVerifySigning__ActiveKid", verifyKid);
    }

    [SkippableFact]
    public async Task Login_third_attempt_on_same_ip_and_email_returns_429_with_retry_after()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string email = "login-rl-user@test.local";
        await SeedUserAsync(email, "rate-limit-user-pw-12345", "user");

        using var client = ClientFromIp("203.0.113.10");
        var first = await client.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        var second = await client.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        var third = await client.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.NotNull(third.Headers.RetryAfter);

        var body = await third.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body);
    }

    [SkippableFact]
    public async Task Login_bucket_is_per_ip_plus_email_same_ip_different_email_is_independent()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string emailA = "login-rl-a@test.local";
        const string emailB = "login-rl-b@test.local";
        await SeedUserAsync(emailA, "rate-limit-a-pw-12345", "user");
        await SeedUserAsync(emailB, "rate-limit-b-pw-12345", "user");

        using var client = ClientFromIp("203.0.113.11");
        // Exhaust emailA's bucket from this IP.
        await client.PostAsJsonAsync("/sessions", new { email = emailA, password = "wrong" });
        await client.PostAsJsonAsync("/sessions", new { email = emailA, password = "wrong" });
        var emailACapped = await client.PostAsJsonAsync("/sessions", new { email = emailA, password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, emailACapped.StatusCode);

        // emailB from the same IP is still free.
        var emailBFirst = await client.PostAsJsonAsync("/sessions", new { email = emailB, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, emailBFirst.StatusCode);
    }

    [SkippableFact]
    public async Task Login_bucket_is_per_ip_plus_email_same_email_different_ip_is_independent()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string email = "login-rl-shared@test.local";
        await SeedUserAsync(email, "rate-limit-shared-pw-12345", "user");

        using var clientIp1 = ClientFromIp("203.0.113.20");
        await clientIp1.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        await clientIp1.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        var capped = await clientIp1.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        using var clientIp2 = ClientFromIp("203.0.113.21");
        var fresh = await clientIp2.PostAsJsonAsync("/sessions", new { email, password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, fresh.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_third_attempt_from_same_ip_returns_429()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client = ClientFromIp("203.0.113.30");
        await client.PostAsJsonAsync("/sessions/refresh", "fake-token-1");
        await client.PostAsJsonAsync("/sessions/refresh", "fake-token-2");
        var third = await client.PostAsJsonAsync("/sessions/refresh", "fake-token-3");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [SkippableFact]
    public async Task Verify_third_attempt_against_same_licence_key_returns_429_but_other_keys_pass()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedProductAndLicenceDirectAsync("rl-verify");

        using var client = ClientFromIp("203.0.113.40");
        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var capped = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        // A different licence key from the same IP is a different bucket.
        var (otherProductId, otherKey) = await SeedProductAndLicenceDirectAsync("rl-verify-other");
        var otherOk = await client.PostAsJsonAsync("/licences/verify", new { licenceKey = otherKey, productId = otherProductId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, otherOk.StatusCode);
    }

    [SkippableFact]
    public async Task Checkout_third_attempt_against_same_key_and_instance_returns_429()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedProductAndLicenceDirectAsync("rl-checkout");
        var instanceId = GenerateInstanceId();

        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
            {
                licenceKey,
                productId,
                clientNonce = GenerateClientNonce(),
                instanceId
            });
            Assert.True(ok.IsSuccessStatusCode, $"Iter {i} expected success, got {ok.StatusCode}");
        }

        var capped = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        var otherInstance = GenerateInstanceId();
        var otherOk = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = otherInstance
        });
        Assert.True(otherOk.IsSuccessStatusCode, $"Different-instance bucket should be independent, got {otherOk.StatusCode}");
    }

    [SkippableFact]
    public async Task Heartbeat_third_attempt_against_same_seat_returns_429()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedProductAndLicenceDirectAsync("rl-heartbeat");
        var open = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        open.EnsureSuccessStatusCode();
        var openBody = await open.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var jwt = await VerifySignedLicencePayloadAsync(openBody!.SignedPayload);
        var seatId = jwt.Claims.Single(c => c.Type == "seatId").Value;

        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await UnauthedClient.PostAsJsonAsync($"/licences/checkouts/{seatId}/heartbeat", new
            {
                clientNonce = GenerateClientNonce()
            });
            Assert.True(ok.IsSuccessStatusCode, $"Iter {i} expected success, got {ok.StatusCode}");
        }

        var capped = await UnauthedClient.PostAsJsonAsync($"/licences/checkouts/{seatId}/heartbeat", new
        {
            clientNonce = GenerateClientNonce()
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);
    }

    [SkippableFact]
    public async Task Checkin_third_attempt_against_same_seat_returns_429()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedProductAndLicenceDirectAsync("rl-checkin");
        var open = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        open.EnsureSuccessStatusCode();
        var openBody = await open.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var jwt = await VerifySignedLicencePayloadAsync(openBody!.SignedPayload);
        var seatId = jwt.Claims.Single(c => c.Type == "seatId").Value;

        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await UnauthedClient.DeleteAsync($"/licences/checkouts/{seatId}");
            Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        }

        var capped = await UnauthedClient.DeleteAsync($"/licences/checkouts/{seatId}");
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    [SkippableFact]
    public async Task Verify_public_key_third_call_from_same_ip_returns_429()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client = ClientFromIp("203.0.113.50");
        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await client.GetAsync("/licences/verify/public-key");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var capped = await client.GetAsync("/licences/verify/public-key");
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_third_call_from_same_user_returns_429_other_user_unaffected()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        // AuthedClient is already admin@test.local, stamped with IP 10.9.0.1, with zero admin-bucket usage.
        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await AuthedClient.GetAsync("/licences");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var capped = await AuthedClient.GetAsync("/licences");
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        // A second admin hits a different user bucket.
        const string secondAdminEmail = "admin-rl-second@test.local";
        const string secondAdminPassword = "second-admin-rl-pw-12345";
        await SeedUserAsync(secondAdminEmail, secondAdminPassword, "admin");
        using var secondAdmin = await CreateLoggedInAsIpAsync(secondAdminEmail, secondAdminPassword, "10.9.0.2");
        var fresh = await secondAdmin.GetAsync("/licences");
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [SkippableFact]
    public async Task Me_endpoints_share_admin_rate_limit_bucket()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string email = "me-rl@test.local";
        const string password = "me-rl-pw-12345";
        await SeedUserAsync(email, password, "user");

        using var client = await CreateLoggedInAsIpAsync(email, password, "10.9.0.50");

        for (var i = 0; i < PermitLimit; i++)
        {
            var ok = await client.GetAsync("/me");
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var capped = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        var siblingCapped = await client.GetAsync("/me/licences");
        Assert.Equal(HttpStatusCode.TooManyRequests, siblingCapped.StatusCode);
    }

    [SkippableFact]
    public async Task Rate_limit_429_body_is_problem_details_with_retry_after_header()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        using var client = ClientFromIp("203.0.113.60");
        await client.GetAsync("/licences/verify/public-key");
        await client.GetAsync("/licences/verify/public-key");
        var capped = await client.GetAsync("/licences/verify/public-key");
        Assert.Equal(HttpStatusCode.TooManyRequests, capped.StatusCode);

        Assert.NotNull(capped.Headers.RetryAfter);
        Assert.Equal("application/problem+json", capped.Content.Headers.ContentType?.MediaType);

        var body = await capped.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body);
    }

    private async Task SeedUserAsync(string email, string password, string role)
    {
        var hasher = new Argon2IdPasswordHasher();
        var hash = hasher.Hash(password);
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
            VALUES (@Id, @Email, @EmailLower, @Hash, NULL, @Role, 'active', NOW(), NOW());
            """,
            new
            {
                Id = Guid.NewGuid(),
                Email = email,
                EmailLower = email.ToLowerInvariant(),
                Hash = hash,
                Role = role
            });
    }

    private async Task<(Guid productId, string licenceKey)> SeedProductAndLicenceDirectAsync(string slug)
    {
        var key = new LicenceKeyGenerator().Generate();
        var pepperPath = Environment.GetEnvironmentVariable("Licence__Peppers__0__Path")
                         ?? throw new InvalidOperationException("Pepper path env var missing.");
        var pepperVersion = short.Parse(Environment.GetEnvironmentVariable("Licence__Peppers__0__Version") ?? "1");
        var pepperText = (await File.ReadAllTextAsync(pepperPath)).Trim();
        var pepper = Convert.FromBase64String(pepperText);
        var pepperSet = new HmacPepperSet(new Dictionary<short, byte[]> { [pepperVersion] = pepper }, pepperVersion);
        var hasher = new HmacLicenceKeyHasher(pepperSet);
        var pepperedHmac = hasher.HashWithActive(key);

        var productId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, slug, display_name, created_at)
            VALUES (@ProductId, @Slug, @Slug, NOW());
            """,
            new { ProductId = productId, Slug = slug });
        await conn.ExecuteAsync(
            """
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, expires_at, notes, hwid_hmac, hwid_hmac_pepper_version, ip_allowlist, max_seats, created_at, updated_at)
            VALUES (@LicenceId, @ProductId, @UserId, @KeyHmac, @KeyHmacPepperVersion, 'active', NULL, NULL, NULL, NULL, NULL, 5, NOW(), NOW());
            """,
            new
            {
                LicenceId = licenceId,
                ProductId = productId,
                UserId = AdminUserId,
                KeyHmac = pepperedHmac.Hmac,
                KeyHmacPepperVersion = pepperedHmac.PepperVersion
            });
        await conn.ExecuteAsync(
            """
            INSERT INTO licence_keys (id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, created_at)
            VALUES (@Id, @LicenceId, @KeyHmac, @KeyHmacPepperVersion, @KeyPrefix, NOW());
            """,
            new
            {
                Id = Guid.NewGuid(),
                LicenceId = licenceId,
                KeyHmac = pepperedHmac.Hmac,
                KeyHmacPepperVersion = pepperedHmac.PepperVersion,
                KeyPrefix = key.Length > 12 ? key[..12] + "..." : key + "..."
            });
        return (productId, key);
    }

    private async Task<HttpClient> CreateLoggedInAsIpAsync(string email, string password, string ip)
    {
        var loginClient = Factory!.CreateClient();
        loginClient.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
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

    private sealed record SessionPayload(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
}
