using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Core.Licences;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenceBackend.Tests.Persistence;

public sealed class LicenceRepositoryMaxSeatsTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task New_licence_defaults_to_max_seats_one()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();

        var licence = await repo.FindByIdAsync(licenceId, CancellationToken.None);

        Assert.NotNull(licence);
        Assert.Equal(1, licence!.MaxSeats);
    }

    [SkippableFact]
    public async Task UpdateMaxSeatsAsync_persists_value_and_writes_audit_event()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();

        var updated = await repo.UpdateMaxSeatsAsync(licenceId, 5, AdminUserId, "test", CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(5, updated!.MaxSeats);

        await using var conn = await OpenDbAsync();
        var audit = await conn.QuerySingleAsync<(string EventType, string Payload)>(
            """
            SELECT event_type, payload::text AS payload
            FROM audit_events
            WHERE subject_id = @LicenceId AND event_type = 'licence.max_seats_updated';
            """,
            new { LicenceId = licenceId });
        Assert.Equal("licence.max_seats_updated", audit.EventType);
        Assert.Contains("\"previousMaxSeats\": 1", audit.Payload);
        Assert.Contains("\"newMaxSeats\": 5", audit.Payload);
    }

    [SkippableFact]
    public async Task UpdateMaxSeatsAsync_returns_null_when_licence_missing()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceRepository>();
        var result = await repo.UpdateMaxSeatsAsync(Guid.NewGuid(), 5, AdminUserId, null, CancellationToken.None);
        Assert.Null(result);
    }

    [SkippableFact]
    public async Task UpdateMaxSeatsAsync_is_noop_when_value_unchanged()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();
        await repo.UpdateMaxSeatsAsync(licenceId, 1, AdminUserId, null, CancellationToken.None);

        await using var conn = await OpenDbAsync();
        var count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.max_seats_updated';",
            new { LicenceId = licenceId });
        Assert.Equal(0, count);
    }

    private async Task<(Guid LicenceId, Guid ProductId, Guid OwnerUserId)> SeedLicenceAsync()
    {
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, slug, display_name) VALUES (@Id, @Slug, @Name);
            INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
              VALUES (@OwnerId, @Email, @EmailLower, 'placeholder-hash', NULL, 'user', 'active', NOW(), NOW());
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, created_at, updated_at)
              VALUES (@LicenceId, @ProductId, @OwnerId, @KeyHmac, @KeyHmacPepperVersion, 'active', NOW(), NOW());
            """,
            new
            {
                Id = productId,
                Slug = $"prod-{productId:N}",
                Name = "Test Product",
                OwnerId = ownerId,
                Email = $"owner-{ownerId:N}@test.local",
                EmailLower = $"owner-{ownerId:N}@test.local",
                LicenceId = licenceId,
                ProductId = productId,
                KeyHmac = RandomNumberGenerator.GetBytes(32),
                KeyHmacPepperVersion = (short)1
            });
        return (licenceId, productId, ownerId);
    }
}
