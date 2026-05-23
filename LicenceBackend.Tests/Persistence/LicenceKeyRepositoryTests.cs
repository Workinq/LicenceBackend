using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Core.Licences;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenceBackend.Tests.Persistence;

public sealed class LicenceKeyRepositoryTests : IntegrationTestBase
{
    private const int Cap = 5;
    private const short PepperVersion = 1;

    [SkippableFact]
    public async Task Mint_inserts_active_key_and_returns_it()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (licenceId, _) = await CreateLicenceAsync();

        var outcome = await repo.MintAsync(
            new MintLicenceKeyParameters(
                licenceId,
                new PepperedHmac(NewRandom(32), PepperVersion),
                KeyPrefix: "LIC-AAAA-...",
                Label: "second-machine",
                CreatedByUserId: AdminUserId,
                ActiveCap: Cap),
            CancellationToken.None);

        var minted = Assert.IsType<MintKeyOutcome.Minted>(outcome);
        Assert.Equal(licenceId, minted.Key.LicenceId);
        Assert.True(minted.Key.IsActive);
        Assert.Equal("second-machine", minted.Key.Label);
        Assert.Equal(AdminUserId, minted.Key.CreatedByUserId);
    }

    [SkippableFact]
    public async Task Mint_rejects_when_active_cap_reached()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (licenceId, _) = await CreateLicenceAsync();

        for (var i = 0; i < Cap - 1; i++)
        {
            var fillOutcome = await repo.MintAsync(
                new MintLicenceKeyParameters(
                    licenceId,
                    new PepperedHmac(NewRandom(32), PepperVersion),
                    KeyPrefix: $"LIC-FILL{i}-...",
                    Label: null,
                    CreatedByUserId: AdminUserId,
                    ActiveCap: Cap),
                CancellationToken.None);
            Assert.IsType<MintKeyOutcome.Minted>(fillOutcome);
        }

        var outcome = await repo.MintAsync(
            new MintLicenceKeyParameters(
                licenceId,
                new PepperedHmac(NewRandom(32), PepperVersion),
                KeyPrefix: "LIC-OVER-...",
                Label: null,
                CreatedByUserId: AdminUserId,
                ActiveCap: Cap),
            CancellationToken.None);

        var capExceeded = Assert.IsType<MintKeyOutcome.CapExceeded>(outcome);
        Assert.Equal(Cap, capExceeded.ActiveCount);
        Assert.Equal(Cap, capExceeded.Cap);
    }

    [SkippableFact]
    public async Task Mint_after_revoke_does_not_count_revoked_keys()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (licenceId, seedKeyId) = await CreateLicenceAsync();

        for (var i = 0; i < Cap - 1; i++)
        {
            var fillOutcome = await repo.MintAsync(
                new MintLicenceKeyParameters(
                    licenceId,
                    new PepperedHmac(NewRandom(32), PepperVersion),
                    KeyPrefix: $"LIC-FILL{i}-...",
                    Label: null,
                    CreatedByUserId: AdminUserId,
                    ActiveCap: Cap),
                CancellationToken.None);
            Assert.IsType<MintKeyOutcome.Minted>(fillOutcome);
        }

        var revoked = await repo.RevokeAsync(seedKeyId, AdminUserId, "rotate", CancellationToken.None);
        Assert.IsType<RevokeKeyOutcome.Revoked>(revoked);

        var outcome = await repo.MintAsync(
            new MintLicenceKeyParameters(
                licenceId,
                new PepperedHmac(NewRandom(32), PepperVersion),
                KeyPrefix: "LIC-NEW-...",
                Label: null,
                CreatedByUserId: AdminUserId,
                ActiveCap: Cap),
            CancellationToken.None);

        Assert.IsType<MintKeyOutcome.Minted>(outcome);
    }

    [SkippableFact]
    public async Task FindActiveByKeyHmac_skips_revoked_rows()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (licenceId, _) = await CreateLicenceAsync();
        var hmac = NewRandom(32);

        var minted = (MintKeyOutcome.Minted)await repo.MintAsync(
            new MintLicenceKeyParameters(
                licenceId,
                new PepperedHmac(hmac, PepperVersion),
                KeyPrefix: "LIC-FIND-...",
                Label: null,
                CreatedByUserId: AdminUserId,
                ActiveCap: Cap),
            CancellationToken.None);

        var found = await repo.FindActiveByKeyHmacAsync(new[] { hmac }, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(minted.Key.Id, found!.Id);

        var revoked = await repo.RevokeAsync(minted.Key.Id, AdminUserId, "test", CancellationToken.None);
        Assert.IsType<RevokeKeyOutcome.Revoked>(revoked);

        var foundAfterRevoke = await repo.FindActiveByKeyHmacAsync(new[] { hmac }, CancellationToken.None);
        Assert.Null(foundAfterRevoke);
    }

    [SkippableFact]
    public async Task UpdateLabel_changes_label_and_returns_updated_row()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (_, seedKeyId) = await CreateLicenceAsync();

        var updated = await repo.UpdateLabelAsync(seedKeyId, "renamed", CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("renamed", updated!.Label);

        await using var conn = await OpenDbAsync();
        var persisted = await conn.QuerySingleAsync<string?>(
            "SELECT label FROM licence_keys WHERE id = @Id;",
            new { Id = seedKeyId });
        Assert.Equal("renamed", persisted);
    }

    [SkippableFact]
    public async Task BumpLastSeen_writes_timestamp()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (_, seedKeyId) = await CreateLicenceAsync();
        var seenAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        await repo.BumpLastSeenAsync(seedKeyId, seenAt, CancellationToken.None);

        await using var conn = await OpenDbAsync();
        var lastSeen = await conn.QuerySingleAsync<DateTime?>(
            "SELECT last_seen_at FROM licence_keys WHERE id = @Id;",
            new { Id = seedKeyId });
        Assert.NotNull(lastSeen);
        Assert.Equal(seenAt.UtcDateTime, DateTime.SpecifyKind(lastSeen!.Value, DateTimeKind.Utc));
    }

    [SkippableFact]
    public async Task Revoke_returns_AlreadyRevoked_on_double_revoke()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();
        var (_, seedKeyId) = await CreateLicenceAsync();

        var first = await repo.RevokeAsync(seedKeyId, AdminUserId, "first", CancellationToken.None);
        Assert.IsType<RevokeKeyOutcome.Revoked>(first);

        var second = await repo.RevokeAsync(seedKeyId, AdminUserId, "second", CancellationToken.None);
        var already = Assert.IsType<RevokeKeyOutcome.AlreadyRevoked>(second);
        Assert.Equal(seedKeyId, already.Key.Id);
    }

    [SkippableFact]
    public async Task Mint_returns_LicenceNotFound_when_licence_does_not_exist()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceKeyRepository>();

        var outcome = await repo.MintAsync(
            new MintLicenceKeyParameters(
                Guid.NewGuid(),
                new PepperedHmac(NewRandom(32), PepperVersion),
                KeyPrefix: "LIC-MISS-...",
                Label: null,
                CreatedByUserId: AdminUserId,
                ActiveCap: Cap),
            CancellationToken.None);

        Assert.IsType<MintKeyOutcome.LicenceNotFound>(outcome);
    }

    private async Task<Guid> CreateProductAsync()
    {
        var productId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            "INSERT INTO products (id, slug, display_name) VALUES (@Id, @Slug, 'Test');",
            new { Id = productId, Slug = $"prod-{productId:N}" });
        return productId;
    }

    private async Task<(Guid LicenceId, Guid SeedKeyId)> CreateLicenceAsync()
    {
        var productId = await CreateProductAsync();
        var ownerId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        var seedKeyId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO users (id, email, email_lower, password_hash, role, status, created_at, updated_at)
              VALUES (@OwnerId, @Email, @EmailLower, 'placeholder', 'user', 'active', NOW(), NOW());
            INSERT INTO licences (id, product_id, user_id, status, max_seats, created_at, updated_at)
              VALUES (@LicenceId, @ProductId, @OwnerId, 'active', 1, NOW(), NOW());
            INSERT INTO licence_keys (id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, created_at)
              VALUES (@KeyId, @LicenceId, @SeedHmac, @PepperVersion, 'LIC-SEED-...', NOW());
            """,
            new
            {
                OwnerId = ownerId,
                Email = $"u-{ownerId:N}@test.local",
                EmailLower = $"u-{ownerId:N}@test.local",
                LicenceId = licenceId,
                ProductId = productId,
                KeyId = seedKeyId,
                SeedHmac = NewRandom(32),
                PepperVersion = PepperVersion
            });
        return (licenceId, seedKeyId);
    }

    private static byte[] NewRandom(int len) => RandomNumberGenerator.GetBytes(len);
}
