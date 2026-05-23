using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using LicenceBackend.Infrastructure.Crypto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LicenceBackend.Tests.Api;

public sealed class PepperRotationTests : IntegrationTestBase
{
    private byte[] _pepperV1Bytes = Array.Empty<byte>();
    private string _pepperV1Path = string.Empty;
    private byte[] _pepperV2Bytes = Array.Empty<byte>();
    private string _pepperV2Path = string.Empty;

    protected override void ApplyPreFactoryEnvironment()
    {
        _pepperV1Bytes = RandomNumberGenerator.GetBytes(32);
        _pepperV2Bytes = RandomNumberGenerator.GetBytes(32);
        _pepperV1Path = Path.Combine(TempDir, "pepper-v1.txt");
        _pepperV2Path = Path.Combine(TempDir, "pepper-v2.txt");
        File.WriteAllText(_pepperV1Path, Convert.ToBase64String(_pepperV1Bytes));
        File.WriteAllText(_pepperV2Path, Convert.ToBase64String(_pepperV2Bytes));

        Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", "1");
        Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", _pepperV1Path);
        Environment.SetEnvironmentVariable("Licence__Peppers__1__Version", "2");
        Environment.SetEnvironmentVariable("Licence__Peppers__1__Path", _pepperV2Path);
        Environment.SetEnvironmentVariable("Licence__ActivePepperVersion", "2");
    }

    [SkippableFact]
    public async Task Licence_hashed_under_v1_verifies_after_rotation_to_v2_active()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedLicenceWithPepperAsync(1);

        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task New_licence_created_via_admin_is_stamped_with_active_pepper_version()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var slug = "rot-pepper-" + Guid.NewGuid().ToString("N")[..8];
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product!.Id, userId = AdminUserId });
        licenceResponse.EnsureSuccessStatusCode();
        var created = await licenceResponse.Content.ReadFromJsonAsync<LicenceCreatedPayload>();

        await using var conn = await OpenDbAsync();
        var row = await conn.QuerySingleAsync<short>("SELECT key_hmac_pepper_version FROM licences WHERE id = @Id;", new { created!.Id });
        Assert.Equal((short)2, row);
    }

    [SkippableFact]
    public async Task Hwid_pinned_under_v1_verifies_after_rotation_using_stored_pepper_version()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string hwid = "device-pre-rotation";
        var (productId, licenceKey) = await SeedLicenceWithPepperAsync(1, (1, hwid));

        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, hwid, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Hwid_first_pin_after_rotation_records_active_pepper_version()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey, licenceId) = await SeedLicenceWithPepperReturningIdAsync(1);

        using var client = Factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, hwid = "fresh-device", clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var pinnedVersion = await conn.QuerySingleOrDefaultAsync<short?>("SELECT hwid_hmac_pepper_version FROM licences WHERE id = @Id;", new { Id = licenceId });
        Assert.Equal((short)2, pinnedVersion);
    }

    [SkippableFact]
    public async Task Hwid_pinned_under_removed_pepper_returns_invalid_licence()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string hwid = "device-pinned-under-removed-pepper";
        var (productId, licenceKey) = await SeedLicenceWithPepperAsync(2, (1, hwid));

        var savedV1Version = Environment.GetEnvironmentVariable("Licence__Peppers__0__Version");
        var savedV1Path = Environment.GetEnvironmentVariable("Licence__Peppers__0__Path");
        var savedV2Version = Environment.GetEnvironmentVariable("Licence__Peppers__1__Version");
        var savedV2Path = Environment.GetEnvironmentVariable("Licence__Peppers__1__Path");
        try
        {
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", "2");
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", _pepperV2Path);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Version", null);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Path", null);

            await using var trimmedFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

            using var client = trimmedFactory.CreateClient();
            var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, hwid, clientNonce = GenerateClientNonce() });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("invalid_licence", body);

            var dataSource = trimmedFactory.Services.GetService<NpgsqlDataSource>();
            if (dataSource is not null) await dataSource.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", savedV1Version);
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", savedV1Path);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Version", savedV2Version);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Path", savedV2Path);
        }
    }

    [SkippableFact]
    public async Task Verify_returns_invalid_licence_when_pepper_version_is_no_longer_configured()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, licenceKey) = await SeedLicenceWithPepperAsync(1);

        // Spin an alternate factory configured with only v2 in the pepper set.
        var savedV1Version = Environment.GetEnvironmentVariable("Licence__Peppers__0__Version");
        var savedV1Path = Environment.GetEnvironmentVariable("Licence__Peppers__0__Path");
        var savedV2Version = Environment.GetEnvironmentVariable("Licence__Peppers__1__Version");
        var savedV2Path = Environment.GetEnvironmentVariable("Licence__Peppers__1__Path");
        try
        {
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", "2");
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", _pepperV2Path);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Version", null);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Path", null);

            await using var trimmedFactory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

            using var client = trimmedFactory.CreateClient();
            var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("invalid_licence", body);

            var dataSource = trimmedFactory.Services.GetService<NpgsqlDataSource>();
            if (dataSource is not null) await dataSource.DisposeAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Version", savedV1Version);
            Environment.SetEnvironmentVariable("Licence__Peppers__0__Path", savedV1Path);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Version", savedV2Version);
            Environment.SetEnvironmentVariable("Licence__Peppers__1__Path", savedV2Path);
        }
    }

    private byte[] PepperFor(short version)
    {
        return version switch
        {
            1 => _pepperV1Bytes,
            2 => _pepperV2Bytes,
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
        };
    }

    private async Task<(Guid ProductId, string LicenceKey)> SeedLicenceWithPepperAsync(
        short version,
        (short Version, string Hwid)? pinnedHwidUnderVersion = null)
    {
        var (productId, licenceKey, _) = await SeedLicenceWithPepperReturningIdAsync(version, pinnedHwidUnderVersion);
        return (productId, licenceKey);
    }

    private async Task<(Guid ProductId, string LicenceKey, Guid LicenceId)> SeedLicenceWithPepperReturningIdAsync(
        short version,
        (short Version, string Hwid)? pinnedHwidUnderVersion = null)
    {
        var pepperSet = new HmacPepperSet(
            new Dictionary<short, byte[]>
            {
                [1] = _pepperV1Bytes,
                [2] = _pepperV2Bytes
            },
            version);

        var keyGen = new LicenceKeyGenerator();
        var hasher = new HmacLicenceKeyHasher(pepperSet);
        var licenceKey = keyGen.Generate();
        var hashed = hasher.HashWithActive(licenceKey);

        byte[]? hwidHmac = null;
        short? hwidVersion = null;
        if (pinnedHwidUnderVersion is { Version: var hv, Hwid: var hwidValue })
        {
            var hwidPepper = PepperFor(hv);
            hwidHmac = HMACSHA256.HashData(hwidPepper, Encoding.UTF8.GetBytes(hwidValue.Trim()));
            hwidVersion = hv;
        }

        var productId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        var slug = "pepper-rot-" + Guid.NewGuid().ToString("N")[..8];

        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            "INSERT INTO products (id, slug, display_name, created_at) VALUES (@Id, @Slug, @Slug, NOW());",
            new { Id = productId, Slug = slug });
        await conn.ExecuteAsync(
            """
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status,
                                  hwid_hmac, hwid_hmac_pepper_version, created_at, updated_at)
            VALUES (@Id, @ProductId, @UserId, @KeyHmac, @KeyHmacPepperVersion, 'active',
                    @HwidHmac, @HwidHmacPepperVersion, NOW(), NOW());
            """,
            new
            {
                Id = licenceId,
                ProductId = productId,
                UserId = AdminUserId,
                KeyHmac = hashed.Hmac,
                KeyHmacPepperVersion = hashed.PepperVersion,
                HwidHmac = hwidHmac,
                HwidHmacPepperVersion = hwidVersion
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
                KeyHmac = hashed.Hmac,
                KeyHmacPepperVersion = hashed.PepperVersion,
                KeyPrefix = licenceKey.Length > 12 ? licenceKey[..12] + "..." : licenceKey + "..."
            });

        return (productId, licenceKey, licenceId);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName);

    private sealed record LicenceCreatedPayload(Guid Id, string LicenceKey);
}
