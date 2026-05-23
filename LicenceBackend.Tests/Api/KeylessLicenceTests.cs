using Dapper;
using LicenceBackend.Core.Licences;
using Microsoft.Extensions.DependencyInjection;

namespace LicenceBackend.Tests.Api;

public sealed class KeylessLicenceTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Licence_can_be_persisted_and_read_back_with_no_key()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, currency) VALUES (@Id, @Slug, 'Keyless Test', 'USD');",
                new { Id = productId, Slug = $"keyless-{productId:N}" });
        }

        var repo = Factory!.Services.GetRequiredService<ILicenceRepository>();

        var now = DateTimeOffset.UtcNow;
        var licence = new Licence(
            Guid.NewGuid(), productId, AdminUserId,
            LicenceStatus.Active, ExpiresAt: null, Notes: null,
            HwidHmac: null, HwidHmacPepperVersion: null, IpAllowlist: null,
            Label: null, MaxSeats: 1, CreatedAt: now, UpdatedAt: now);

        await repo.CreateAsync(licence, CancellationToken.None);

        var loaded = await repo.FindByIdAsync(licence.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(licence.Id, loaded.Id);
    }
}
