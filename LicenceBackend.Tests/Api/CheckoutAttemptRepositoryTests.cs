using Dapper;
using LicenceBackend.Core.Payments;
using LicenceBackend.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LicenceBackend.Tests.Api;

public sealed class CheckoutAttemptRepositoryTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Create_then_find_round_trips_attempt_and_items()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var dataSource = Factory!.Services.GetRequiredService<NpgsqlDataSource>();
        var repo = new CheckoutAttemptRepository(dataSource);

        var productId = Guid.NewGuid();
        await using (var conn = await OpenDbAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO products (id, slug, display_name, currency) VALUES (@Id, @Slug, 'Attempt Test', 'USD');",
                new { Id = productId, Slug = $"attempt-{productId:N}" });
        }

        var attemptId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var attempt = new CheckoutAttempt(
            attemptId, AdminUserId, "buyer@test.local", "USD", 19.98m,
            $"pi_test_{attemptId:N}", CheckoutAttemptStatus.Pending, null, now, null);
        var items = new List<CheckoutAttemptItem>
        {
            new(Guid.NewGuid(), attemptId, productId, 2, new string?[] { "alpha", null }, 9.99m, "USD")
        };

        await repo.CreateAsync(attempt, items, CancellationToken.None);

        var found = await repo.FindByIdAsync(attemptId, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(CheckoutAttemptStatus.Pending, found.Status);
        Assert.Equal(19.98m, found.AmountTotal);

        var id = await repo.FindIdByPaymentIntentIdAsync(attempt.StripePaymentIntentId, CancellationToken.None);
        Assert.Equal(attemptId, id);
    }
}
