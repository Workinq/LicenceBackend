using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Core.Licences;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenceBackend.Tests.Persistence;

public sealed class LicenceCheckoutRepositoryTests : IntegrationTestBase
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    [SkippableFact]
    public async Task OpenAsync_inserts_a_seat_when_capacity_available()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 2);
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        var outcome = await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        var opened = Assert.IsType<OpenCheckoutOutcome.Opened>(outcome);
        Assert.Equal(licenceId, opened.Result.Checkout.LicenceId);
        Assert.Equal(1, opened.Result.SeatsAfter);
        Assert.Equal(2, opened.Result.MaxSeats);
        Assert.False(opened.Result.IsIdempotentReplay);
        Assert.True(opened.Result.Checkout.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [SkippableFact]
    public async Task OpenAsync_returns_existing_seat_on_idempotent_replay()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 1);
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        var first = (OpenCheckoutOutcome.Opened)await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);
        var second = (OpenCheckoutOutcome.Opened)await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        Assert.Equal(first.Result.Checkout.Id, second.Result.Checkout.Id);
        Assert.True(second.Result.IsIdempotentReplay);
        Assert.Equal(1, second.Result.SeatsAfter);
    }

    [SkippableFact]
    public async Task OpenAsync_returns_DeniedNoSeats_when_at_capacity()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 1);
        var hashA = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var hashB = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        await repo.OpenAsync(licenceId, hashA, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);
        var outcome = await repo.OpenAsync(licenceId, hashB, null, null, null, "10.0.0.2", null, Lease, CancellationToken.None);

        var denied = Assert.IsType<OpenCheckoutOutcome.DeniedNoSeats>(outcome);
        Assert.Equal(1, denied.Detail.ActiveSeats);
        Assert.Equal(1, denied.Detail.MaxSeats);
    }

    [SkippableFact]
    public async Task OpenAsync_reclaims_expired_seats_before_checking_capacity()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 1);
        var hashOld = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var hashNew = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, source_ip, issued_at, last_heartbeat_at, expires_at)
                VALUES (@Id, @LicenceId, @Hash, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute');
                """,
                new { Id = Guid.NewGuid(), LicenceId = licenceId, Hash = hashOld });
        }

        var outcome = await repo.OpenAsync(licenceId, hashNew, null, null, null, "10.0.0.2", null, Lease, CancellationToken.None);

        var opened = Assert.IsType<OpenCheckoutOutcome.Opened>(outcome);
        Assert.Equal(1, opened.Result.SeatsAfter);

        await using var conn2 = await OpenDbAsync();
        var historyReason = await conn2.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE licence_id = @LicenceId AND instance_id_hash = @Hash;",
            new { LicenceId = licenceId, Hash = hashOld });
        Assert.Equal("expired", historyReason);
    }

    [SkippableFact]
    public async Task OpenAsync_returns_LicenceNotFound_when_licence_missing()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        var outcome = await repo.OpenAsync(Guid.NewGuid(), hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        Assert.IsType<OpenCheckoutOutcome.LicenceNotFound>(outcome);
    }

    [SkippableFact]
    public async Task OpenAsync_writes_an_audit_event_on_success()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 2);
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        await using var conn = await OpenDbAsync();
        var eventType = await conn.QuerySingleAsync<string>(
            "SELECT event_type FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_opened';",
            new { LicenceId = licenceId });
        Assert.Equal("licence.checkout_opened", eventType);
    }

    [SkippableFact]
    public async Task OpenAsync_writes_denied_audit_event_when_capacity_full()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync(maxSeats: 1);
        var hashA = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var hashB = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        await repo.OpenAsync(licenceId, hashA, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);
        await repo.OpenAsync(licenceId, hashB, null, null, null, "10.0.0.2", null, Lease, CancellationToken.None);

        await using var conn = await OpenDbAsync();
        var count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_denied_no_seats';",
            new { LicenceId = licenceId });
        Assert.Equal(1, count);
    }

    [SkippableFact]
    public async Task HeartbeatAsync_extends_lease_and_returns_refreshed_checkout()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var opened = (OpenCheckoutOutcome.Opened)await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, TimeSpan.FromMinutes(1), CancellationToken.None);
        var initialExpiry = opened.Result.Checkout.ExpiresAt;

        await Task.Delay(50);
        var refreshed = await repo.HeartbeatAsync(opened.Result.Checkout.Id, TimeSpan.FromMinutes(10), CancellationToken.None);

        Assert.NotNull(refreshed);
        Assert.Equal(opened.Result.Checkout.Id, refreshed!.Id);
        Assert.True(refreshed.ExpiresAt > initialExpiry);
    }

    [SkippableFact]
    public async Task HeartbeatAsync_returns_null_for_missing_seat()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var refreshed = await repo.HeartbeatAsync(Guid.NewGuid(), TimeSpan.FromMinutes(10), CancellationToken.None);
        Assert.Null(refreshed);
    }

    [SkippableFact]
    public async Task HeartbeatAsync_returns_null_for_expired_seat()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var checkoutId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, source_ip, issued_at, last_heartbeat_at, expires_at)
                VALUES (@Id, @LicenceId, @Hash, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute');
                """,
                new { Id = checkoutId, LicenceId = licenceId, Hash = hash });
        }

        var refreshed = await repo.HeartbeatAsync(checkoutId, TimeSpan.FromMinutes(10), CancellationToken.None);

        Assert.Null(refreshed);
    }

    [SkippableFact]
    public async Task CloseAsync_archives_seat_with_checkin_reason_and_no_audit()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var opened = (OpenCheckoutOutcome.Opened)await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        var closed = await repo.CloseAsync(opened.Result.Checkout.Id, CancellationToken.None);

        Assert.True(closed);

        await using var conn = await OpenDbAsync();
        var liveCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM licence_checkouts WHERE id = @Id;",
            new { Id = opened.Result.Checkout.Id });
        Assert.Equal(0, liveCount);

        var historyReason = await conn.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE checkout_id = @Id;",
            new { Id = opened.Result.Checkout.Id });
        Assert.Equal("checkin", historyReason);

        var auditCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_closed';",
            new { LicenceId = licenceId });
        Assert.Equal(0, auditCount);
    }

    [SkippableFact]
    public async Task CloseAsync_returns_false_for_missing_seat()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var result = await repo.CloseAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.False(result);
    }

    [SkippableFact]
    public async Task ForceRevokeAsync_archives_seat_and_writes_audit_event()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var opened = (OpenCheckoutOutcome.Opened)await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);

        var revoked = await repo.ForceRevokeAsync(
            opened.Result.Checkout.Id,
            LicenceCheckoutCloseReason.AdminRevoked,
            AdminUserId,
            "abuse",
            CancellationToken.None);

        Assert.True(revoked);

        await using var conn = await OpenDbAsync();
        var historyReason = await conn.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE checkout_id = @Id;",
            new { Id = opened.Result.Checkout.Id });
        Assert.Equal("admin_revoked", historyReason);

        var auditRow = await conn.QuerySingleAsync<(string EventType, string Reason)>(
            "SELECT event_type, reason FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_closed';",
            new { LicenceId = licenceId });
        Assert.Equal("licence.checkout_closed", auditRow.EventType);
        Assert.Equal("abuse", auditRow.Reason);

        var payload = await conn.QuerySingleAsync<string>(
            "SELECT payload::text FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_closed';",
            new { LicenceId = licenceId });
        Assert.Contains("\"sourceIp\": \"10.0.0.1/32\"", payload);
        Assert.Contains("\"closeReason\": \"admin_revoked\"", payload);
    }

    [SkippableFact]
    public async Task ForceRevokeAsync_returns_false_for_missing_seat()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var result = await repo.ForceRevokeAsync(Guid.NewGuid(), LicenceCheckoutCloseReason.AdminRevoked, AdminUserId, null, CancellationToken.None);
        Assert.False(result);
    }

    [SkippableFact]
    public async Task ReclaimExpiredAsync_archives_only_expired_seats()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceA, _, _) = await SeedLicenceAsync();
        var (licenceB, _, _) = await SeedLicenceAsync();

        var expiredHashA = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var expiredHashB = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var liveHash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, source_ip, issued_at, last_heartbeat_at, expires_at)
                VALUES
                    (gen_random_uuid(), @A, @ExpA, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute'),
                    (gen_random_uuid(), @B, @ExpB, '10.0.0.1'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute'),
                    (gen_random_uuid(), @A, @Live, '10.0.0.1'::inet, NOW(), NOW(), NOW() + INTERVAL '10 minutes');
                """,
                new { A = licenceA, B = licenceB, ExpA = expiredHashA, ExpB = expiredHashB, Live = liveHash });
        }

        var result = await repo.ReclaimExpiredAsync(DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Equal(2, result.ReclaimedCount);
        Assert.Equal(2, result.LicencesAffected);

        await using var conn2 = await OpenDbAsync();
        var liveStillThere = await conn2.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM licence_checkouts WHERE instance_id_hash = @Hash;",
            new { Hash = liveHash });
        Assert.Equal(1, liveStillThere);

        var archivedCount = await conn2.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM licence_checkout_history WHERE close_reason = 'expired' AND instance_id_hash IN (@ExpA, @ExpB);",
            new { ExpA = expiredHashA, ExpB = expiredHashB });
        Assert.Equal(2, archivedCount);
    }

    [SkippableFact]
    public async Task ReclaimExpiredAsync_returns_zero_when_nothing_expired()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var result = await repo.ReclaimExpiredAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(0, result.ReclaimedCount);
        Assert.Equal(0, result.LicencesAffected);
    }

    [SkippableFact]
    public async Task ListLiveForLicenceAsync_returns_only_unexpired_seats_for_that_licence()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceA, _, _) = await SeedLicenceAsync(maxSeats: 3);
        var (licenceB, _, _) = await SeedLicenceAsync();
        var hashA1 = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var hashA2 = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var hashB = SHA256.HashData(RandomNumberGenerator.GetBytes(32));

        await repo.OpenAsync(licenceA, hashA1, null, null, null, "10.0.0.1", null, Lease, CancellationToken.None);
        await repo.OpenAsync(licenceA, hashA2, null, null, null, "10.0.0.2", null, Lease, CancellationToken.None);
        await repo.OpenAsync(licenceB, hashB, null, null, null, "10.0.0.3", null, Lease, CancellationToken.None);

        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO licence_checkouts (id, licence_id, instance_id_hash, source_ip, issued_at, last_heartbeat_at, expires_at) VALUES (gen_random_uuid(), @L, @H, '10.0.0.4'::inet, NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '20 minutes', NOW() - INTERVAL '1 minute');",
                new { L = licenceA, H = SHA256.HashData(RandomNumberGenerator.GetBytes(32)) });
        }

        var live = await repo.ListLiveForLicenceAsync(licenceA, CancellationToken.None);

        Assert.Equal(2, live.Count);
        Assert.All(live, c => Assert.Equal(licenceA, c.LicenceId));
        Assert.All(live, c => Assert.True(c.ExpiresAt > DateTimeOffset.UtcNow));
    }

    [SkippableFact]
    public async Task ListHistoryForLicenceAsync_returns_archived_seats_newest_first()
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var (licenceId, _, _) = await SeedLicenceAsync();

        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO licence_checkout_history (id, licence_id, checkout_id, instance_id_hash, source_ip, issued_at, closed_at, close_reason)
                VALUES
                    (gen_random_uuid(), @L, gen_random_uuid(), @H1, '10.0.0.1'::inet, NOW() - INTERVAL '2 hours', NOW() - INTERVAL '1 hour', 'checkin'),
                    (gen_random_uuid(), @L, gen_random_uuid(), @H2, '10.0.0.1'::inet, NOW() - INTERVAL '30 minutes', NOW() - INTERVAL '5 minutes', 'expired');
                """,
                new
                {
                    L = licenceId,
                    H1 = SHA256.HashData(RandomNumberGenerator.GetBytes(32)),
                    H2 = SHA256.HashData(RandomNumberGenerator.GetBytes(32))
                });
        }

        var page = await repo.ListHistoryForLicenceAsync(licenceId, 20, 0, CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.Items[0].ClosedAt > page.Items[1].ClosedAt);
        Assert.Equal(LicenceCheckoutCloseReason.Expired, page.Items[0].CloseReason);
        Assert.Equal(LicenceCheckoutCloseReason.Checkin, page.Items[1].CloseReason);
    }

    [SkippableFact]
    public async Task ForceRevokeByLicenceKey_closes_all_live_seats_for_that_key_and_writes_history()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var (licenceId, keyId) = await SeedLicenceWithKeyAsync();
        await OpenLiveCheckoutAsync(licenceId, keyId);
        await OpenLiveCheckoutAsync(licenceId, keyId);

        var checkouts = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var closed = await checkouts.ForceRevokeByLicenceKeyAsync(keyId, AdminUserId, "leaked", CancellationToken.None);

        Assert.Equal(2, closed);

        await using var db = await OpenDbAsync();
        var liveCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM licence_checkouts WHERE issued_with_licence_key_id = @KeyId;",
            new { KeyId = keyId });
        Assert.Equal(0, liveCount);

        var historyCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM licence_checkout_history WHERE licence_id = @Id AND close_reason = 'key_revoked';",
            new { Id = licenceId });
        Assert.Equal(2, historyCount);

        var auditCount = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @Id AND event_type = 'licence.checkout_closed';",
            new { Id = licenceId });
        Assert.Equal(2, auditCount);
    }

    private async Task<(Guid LicenceId, Guid KeyId)> SeedLicenceWithKeyAsync()
    {
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, slug, display_name) VALUES (@ProductId, @Slug, 'Test');
            INSERT INTO users (id, email, email_lower, password_hash, role, status, created_at, updated_at)
              VALUES (@OwnerId, @Email, @EmailLower, 'placeholder', 'user', 'active', NOW(), NOW());
            INSERT INTO licences (id, product_id, user_id, key_hmac, key_hmac_pepper_version, status, max_seats, created_at, updated_at)
              VALUES (@LicenceId, @ProductId, @OwnerId, NULL, NULL, 'active', 5, NOW(), NOW());
            INSERT INTO licence_keys (id, licence_id, key_hmac, key_hmac_pepper_version, key_prefix, created_at)
              VALUES (@KeyId, @LicenceId, @SeedHmac, @PepperVersion, 'LIC-SEED-...', NOW());
            """,
            new
            {
                ProductId = productId,
                Slug = $"prod-{productId:N}",
                OwnerId = ownerId,
                Email = $"u-{ownerId:N}@test.local",
                EmailLower = $"u-{ownerId:N}@test.local",
                LicenceId = licenceId,
                KeyId = keyId,
                SeedHmac = RandomNumberGenerator.GetBytes(32),
                PepperVersion = (short)1
            });
        return (licenceId, keyId);
    }

    private async Task OpenLiveCheckoutAsync(Guid licenceId, Guid keyId)
    {
        var repo = Factory!.Services.GetRequiredService<ILicenceCheckoutRepository>();
        var hash = SHA256.HashData(RandomNumberGenerator.GetBytes(32));
        var outcome = await repo.OpenAsync(licenceId, hash, null, null, null, "10.0.0.1", keyId, Lease, CancellationToken.None);
        Assert.IsType<OpenCheckoutOutcome.Opened>(outcome);
    }

    internal async Task<(Guid LicenceId, Guid ProductId, Guid OwnerUserId)> SeedLicenceAsync(int maxSeats = 1)
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
              VALUES (@LicenceId, @ProductId, @OwnerId, @KeyHmac, @KeyHmacPepperVersion, 'active', @MaxSeats, NOW(), NOW());
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
                KeyHmacPepperVersion = (short)1,
                MaxSeats = maxSeats
            });
        return (licenceId, productId, ownerId);
    }
}
