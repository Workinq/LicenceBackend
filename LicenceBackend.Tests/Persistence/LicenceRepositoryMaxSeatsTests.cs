using LicenceBackend.Core.Licences;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LicenceBackend.Tests.Persistence;

public class LicenceRepositoryMaxSeatsTests : IntegrationTestBase
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

    private async Task<(Guid LicenceId, Guid ProductId, Guid OwnerUserId)> SeedLicenceAsync()
    {
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var licenceId = Guid.NewGuid();
        await using var conn = await OpenDbAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            """
            INSERT INTO products (id, slug, display_name) VALUES (@Id, @Slug, @Name);
            INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
              VALUES (@OwnerId, @Email, @EmailLower, 'placeholder-hash', NULL, 'user', 'active', NOW(), NOW());
            INSERT INTO licences (id, product_id, user_id, key_hmac, status, created_at, updated_at)
              VALUES (@LicenceId, @ProductId, @OwnerId, @KeyHmac, 'active', NOW(), NOW());
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
                KeyHmac = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)
            });
        return (licenceId, productId, ownerId);
    }
}
