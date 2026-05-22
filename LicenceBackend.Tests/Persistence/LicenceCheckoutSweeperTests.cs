using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Core.Licences;
using LicenceBackend.Infrastructure.Hosting;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenceBackend.Tests.Persistence;

public sealed class LicenceCheckoutSweeperTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task SweepOnceAsync_with_no_expired_seats_writes_no_audit_row()
    {
        var sweeper = Factory!.Services.GetRequiredService<LicenceCheckoutSweeper>();

        var result = await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.ReclaimedCount);
        Assert.Equal(0, result.LicencesAffected);

        await using var conn = await OpenDbAsync();
        var auditCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE event_type = 'licence.checkout_sweeper_ran';");
        Assert.Equal(0, auditCount);
    }

    [SkippableFact]
    public async Task SweepOnceAsync_with_expired_seats_archives_and_writes_audit_row()
    {
        var (licenceA, _, _) = await SeedLicenceAsync();
        var (licenceB, _, _) = await SeedLicenceAsync();

        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, source_ip, issued_at, last_heartbeat_at, expires_at)
                VALUES
                    (gen_random_uuid(), @A, @H1, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute'),
                    (gen_random_uuid(), @A, @H2, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute'),
                    (gen_random_uuid(), @B, @H3, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute');
                """,
                new
                {
                    A = licenceA,
                    B = licenceB,
                    H1 = SHA256.HashData(RandomNumberGenerator.GetBytes(32)),
                    H2 = SHA256.HashData(RandomNumberGenerator.GetBytes(32)),
                    H3 = SHA256.HashData(RandomNumberGenerator.GetBytes(32))
                });
        }

        var sweeper = Factory!.Services.GetRequiredService<LicenceCheckoutSweeper>();
        var result = await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(3, result.ReclaimedCount);
        Assert.Equal(2, result.LicencesAffected);

        await using var conn2 = await OpenDbAsync();
        var auditRow = await conn2.QuerySingleAsync<(string EventType, string SubjectType, Guid SubjectId, string ActorType, string Payload)>(
            "SELECT event_type, subject_type, subject_id, actor_type, payload::text AS payload FROM audit_events WHERE event_type = 'licence.checkout_sweeper_ran';");
        Assert.Equal("licence.checkout_sweeper_ran", auditRow.EventType);
        Assert.Equal("system", auditRow.SubjectType);
        Assert.Equal(Guid.Empty, auditRow.SubjectId);
        Assert.Equal("system", auditRow.ActorType);
        Assert.Contains("\"reclaimedCount\": 3", auditRow.Payload);
        Assert.Contains("\"licencesAffected\": 2", auditRow.Payload);
    }

    [SkippableFact]
    public async Task SweepOnceAsync_returns_zero_when_table_empty()
    {
        var sweeper = Factory!.Services.GetRequiredService<LicenceCheckoutSweeper>();
        var result = await sweeper.SweepOnceAsync(CancellationToken.None);
        Assert.Equal(0, result.ReclaimedCount);
        Assert.Equal(0, result.LicencesAffected);
    }

    internal async Task<(Guid LicenceId, Guid ProductId, Guid OwnerUserId)> SeedLicenceAsync()
    {
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, slug, display_name) VALUES (@ProductId, @Slug, 'Test');
            INSERT INTO users (id, email, email_lower, password_hash, role, status, created_at, updated_at)
              VALUES (@OwnerId, @Email, @EmailLower, 'placeholder', 'user', 'active', NOW(), NOW());
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, max_seats, created_at, updated_at)
              VALUES (@LicenceId, @ProductId, @OwnerId, @KeyHmac, @KeyHmacPepperVersion, 'active', 1, NOW(), NOW());
            """,
            new
            {
                ProductId = productId,
                Slug = $"prod-{productId:N}",
                OwnerId = ownerId,
                Email = $"u-{ownerId:N}@test.local",
                EmailLower = $"u-{ownerId:N}@test.local",
                LicenceId = licenceId,
                KeyHmac = RandomNumberGenerator.GetBytes(32),
                KeyHmacPepperVersion = (short)1
            });
        return (licenceId, productId, ownerId);
    }
}
