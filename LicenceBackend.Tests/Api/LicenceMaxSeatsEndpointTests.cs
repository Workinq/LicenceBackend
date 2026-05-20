using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceMaxSeatsEndpointTests : IntegrationTestBase
{
    private const string OwnerPassword = "Owner-password-123!";

    [SkippableFact]
    public async Task PatchMaxSeats_admin_updates_value_and_writes_audit()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync();

        var response = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/max-seats", new
        {
            maxSeats = 5,
            reason = "team-expanded"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var maxSeats = await conn.QuerySingleAsync<int>(
            "SELECT max_seats FROM licences WHERE id = @Id;",
            new { Id = licenceId });
        Assert.Equal(5, maxSeats);

        var auditCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @Id AND event_type = 'licence.max_seats_updated';",
            new { Id = licenceId });
        Assert.Equal(1, auditCount);
    }

    [SkippableFact]
    public async Task PatchMaxSeats_returns_404_for_missing_licence()
    {
        var response = await AuthedClient.PatchAsJsonAsync($"/licences/{Guid.NewGuid()}/max-seats", new
        {
            maxSeats = 5
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task PatchMaxSeats_returns_400_for_zero_or_negative()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync();

        var responseZero = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/max-seats", new
        {
            maxSeats = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, responseZero.StatusCode);

        var responseNegative = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/max-seats", new
        {
            maxSeats = -1
        });
        Assert.Equal(HttpStatusCode.BadRequest, responseNegative.StatusCode);
    }

    [SkippableFact]
    public async Task PatchMaxSeats_reduce_below_active_keeps_existing_seats_but_blocks_new()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(maxSeats: 3);
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var patch = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/max-seats", new
        {
            maxSeats = 1,
            reason = "reduce"
        });
        patch.EnsureSuccessStatusCode();

        await using var conn = await OpenDbAsync();
        var live = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM licence_checkouts WHERE licence_id = @Id;",
            new { Id = licenceId });
        Assert.Equal(3, live);

        var newCheckout = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        Assert.Equal(HttpStatusCode.Conflict, newCheckout.StatusCode);
    }

    [SkippableFact]
    public async Task PatchMaxSeats_non_admin_returns_403()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync(freshOwner: true);
        var nonAdminEmail = $"user-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(nonAdminEmail, OwnerPassword);
        var nonAdminClient = await CreateLoggedInClientAsync(nonAdminEmail, OwnerPassword);

        var response = await nonAdminClient.PatchAsJsonAsync($"/licences/{licenceId}/max-seats", new
        {
            maxSeats = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task OpenCheckoutAsync(string licenceKey, Guid productId, string instanceId)
    {
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateUserAsync(string email, string password)
    {
        var response = await AuthedClient.PostAsJsonAsync("/users", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        return body!.Id;
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    internal async Task<(string LicenceKey, Guid LicenceId, Guid ProductId, Guid OwnerId)> CreateLicenceAsync(int maxSeats = 1, bool freshOwner = false)
    {
        var productSlug = $"prod-{Guid.NewGuid():N}".Substring(0, 24);
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new
        {
            slug = productSlug,
            displayName = "Test Product"
        });
        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();

        Guid ownerId;
        if (freshOwner)
        {
            var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
            ownerId = await CreateUserAsync(ownerEmail, OwnerPassword);
        }
        else
        {
            ownerId = AdminUserId;
        }

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new
        {
            productId = product!.Id,
            userId = ownerId,
            maxSeats
        });
        licenceResponse.EnsureSuccessStatusCode();
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();

        return (licence!.LicenceKey, licence.Id, product.Id, ownerId);
    }

    private sealed record ProductPayload(Guid Id);
    private sealed record UserPayload(Guid Id);
    private sealed record LicencePayload(Guid Id, string LicenceKey);
}
