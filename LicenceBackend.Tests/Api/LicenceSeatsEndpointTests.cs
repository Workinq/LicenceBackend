using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Api.Models.Response;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceSeatsEndpointTests : IntegrationTestBase
{
    private const string OwnerPassword = "Owner-password-123!";
    private readonly Dictionary<Guid, string> _emailByUserId = new();

    [SkippableFact]
    public async Task GetSeats_admin_returns_live_seats_and_history()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(maxSeats: 2);
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        var seatB = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        await CheckinAsync(seatB);

        var response = await AuthedClient.GetAsync($"/licences/{licenceId}/seats");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LicenceSeatsResponse>();

        Assert.NotNull(body);
        Assert.Equal(2, body!.MaxSeats);
        Assert.Single(body.Live);
        Assert.Equal(1, body.History.Total);
        Assert.Equal("checkin", body.History.Items[0].CloseReason);
    }

    [SkippableFact]
    public async Task GetSeats_owner_returns_live_seats_and_history()
    {
        var (licenceKey, licenceId, productId, ownerId) = await CreateLicenceAsync(freshOwner: true);
        var ownerClient = await CreateLoggedInClientAsync(_emailByUserId[ownerId], OwnerPassword);
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var response = await ownerClient.GetAsync($"/licences/{licenceId}/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceSeatsResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Live);
    }

    [SkippableFact]
    public async Task GetSeats_member_returns_live_seats_and_history()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(freshOwner: true);
        var memberEmail = $"member-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(memberEmail, OwnerPassword);
        await AddLicenceMemberAsync(licenceId, memberEmail);
        var memberClient = await CreateLoggedInClientAsync(memberEmail, OwnerPassword);
        await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var response = await memberClient.GetAsync($"/licences/{licenceId}/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceSeatsResponse>();
        Assert.NotNull(body);
        Assert.Single(body!.Live);
    }

    [SkippableFact]
    public async Task GetSeats_unrelated_user_returns_404()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync(freshOwner: true);
        var outsiderEmail = $"outsider-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(outsiderEmail, OwnerPassword);
        var outsiderClient = await CreateLoggedInClientAsync(outsiderEmail, OwnerPassword);

        var response = await outsiderClient.GetAsync($"/licences/{licenceId}/seats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetSeats_unauthenticated_returns_401()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync();

        var response = await UnauthedClient.GetAsync($"/licences/{licenceId}/seats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task ForceRevoke_admin_archives_seat_with_admin_revoked_reason()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync();
        var seatId = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var response = await AuthedClient.DeleteAsync($"/licences/{licenceId}/seats/{seatId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var historyReason = await conn.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE checkout_id = @Id;",
            new { Id = seatId });
        Assert.Equal("admin_revoked", historyReason);
    }

    [SkippableFact]
    public async Task ForceRevoke_owner_archives_seat_with_owner_revoked_reason()
    {
        var (licenceKey, licenceId, productId, ownerId) = await CreateLicenceAsync(freshOwner: true);
        var seatId = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        var ownerClient = await CreateLoggedInClientAsync(_emailByUserId[ownerId], OwnerPassword);

        var response = await ownerClient.DeleteAsync($"/licences/{licenceId}/seats/{seatId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var historyReason = await conn.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE checkout_id = @Id;",
            new { Id = seatId });
        Assert.Equal("owner_revoked", historyReason);
    }

    [SkippableFact]
    public async Task ForceRevoke_member_returns_403()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(freshOwner: true);
        var memberEmail = $"member-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(memberEmail, OwnerPassword);
        await AddLicenceMemberAsync(licenceId, memberEmail);
        var memberClient = await CreateLoggedInClientAsync(memberEmail, OwnerPassword);
        var seatId = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var response = await memberClient.DeleteAsync($"/licences/{licenceId}/seats/{seatId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task ForceRevoke_unrelated_user_returns_404()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(freshOwner: true);
        var seatId = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());
        var outsiderEmail = $"outsider-{Guid.NewGuid():N}@test.local";
        await CreateUserAsync(outsiderEmail, OwnerPassword);
        var outsiderClient = await CreateLoggedInClientAsync(outsiderEmail, OwnerPassword);

        var response = await outsiderClient.DeleteAsync($"/licences/{licenceId}/seats/{seatId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task ForceRevoke_missing_seat_returns_404()
    {
        var (_, licenceId, _, _) = await CreateLicenceAsync();
        var response = await AuthedClient.DeleteAsync($"/licences/{licenceId}/seats/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task ForceRevoke_writes_audit_event()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync();
        var seatId = await OpenCheckoutAsync(licenceKey, productId, GenerateInstanceId());

        var response = await AuthedClient.DeleteAsync($"/licences/{licenceId}/seats/{seatId}");
        response.EnsureSuccessStatusCode();

        await using var conn = await OpenDbAsync();
        var auditCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM audit_events WHERE subject_id = @LicenceId AND event_type = 'licence.checkout_closed';",
            new { LicenceId = licenceId });
        Assert.Equal(1, auditCount);
    }

    private async Task<Guid> OpenCheckoutAsync(string licenceKey, Guid productId, string instanceId)
    {
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var jwt = await VerifySignedLicencePayloadAsync(body!.SignedPayload);
        return Guid.Parse(jwt.Claims.Single(c => c.Type == "seatId").Value);
    }

    private async Task CheckinAsync(Guid seatId)
    {
        var response = await UnauthedClient.DeleteAsync($"/licences/checkouts/{seatId}");
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateUserAsync(string email, string password)
    {
        var response = await AuthedClient.PostAsJsonAsync("/users", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        _emailByUserId[body!.Id] = email;
        return body.Id;
    }

    private async Task AddLicenceMemberAsync(Guid licenceId, string memberEmail)
    {
        var response = await AuthedClient.PostAsJsonAsync($"/licences/{licenceId}/members", new
        {
            email = memberEmail
        });
        response.EnsureSuccessStatusCode();
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    private async Task<(string LicenceKey, Guid LicenceId, Guid ProductId, Guid OwnerId)> CreateLicenceAsync(int maxSeats = 1, bool freshOwner = false)
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
