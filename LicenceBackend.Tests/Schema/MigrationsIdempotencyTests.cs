using LicenceBackend.Infrastructure.Persistence;
using LicenceBackend.Tests.Api;
using Microsoft.Extensions.Logging.Abstractions;

namespace LicenceBackend.Tests.Schema;

public sealed class MigrationsIdempotencyTests : IntegrationTestBase
{
    [SkippableFact]
    public void Migrations_second_run_against_already_migrated_db_is_noop()
    {
        var migrationsDir = FindRepoDirectory("migrations");
        var result        = SchemaMigrator.Run(ConnectionString, migrationsDir, NullLogger.Instance);

        Assert.True(result.Successful, $"Migrator failed: {result.Error}");
        Assert.Empty(result.Scripts);
    }
}
