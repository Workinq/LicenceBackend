using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using LicenceBackend.Api.Models.Response;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceCheckoutEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Checkout_returns_signed_payload_on_success()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync(maxSeats: 2);

        var body = new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        };
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrEmpty(payload!.SignedPayload));

        var jwt = await VerifySignedLicencePayloadAsync(payload.SignedPayload);
        Assert.Equal(licenceId.ToString(), jwt.Claims.Single(c => c.Type == "licenceId").Value);
        Assert.Equal(productId.ToString(), jwt.Claims.Single(c => c.Type == "productId").Value);
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatId"));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatExpiresAt"));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatHeartbeatAfter"));
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_unknown_key()
    {
        var (_, _, productId, _) = await CreateLicenceAsync();
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey = "LIC-00000-00000-00000-00000-00000",
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_product_mismatch()
    {
        var (licenceKey, _, _, _) = await CreateLicenceAsync();
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId = Guid.NewGuid(),
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_too_short_nonce()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync();
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = "short",
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_too_short_instance_id()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync();
        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = "x"
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_suspended_licence()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync();
        var statusResponse = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new
        {
            status = "suspended",
            reason = "test"
        });
        statusResponse.EnsureSuccessStatusCode();

        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_for_suspended_owner()
    {
        var (licenceKey, _, productId, ownerId) = await CreateLicenceAsync(freshOwner: true);
        var statusResponse = await AuthedClient.PatchAsJsonAsync($"/users/{ownerId}/status", new
        {
            status = "suspended",
            reason = "test"
        });
        statusResponse.EnsureSuccessStatusCode();

        var response = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_invalid_licence_when_ip_not_allowlisted()
    {
        var (licenceKey, licenceId, productId, _) = await CreateLicenceAsync();
        var ipResponse = await AuthedClient.PutAsJsonAsync($"/licences/{licenceId}/ip-allowlist", new
        {
            cidrs = new[] { "192.168.0.0/24" },
            reason = "test"
        });
        ipResponse.EnsureSuccessStatusCode();

        var client = ClientFromIp("10.0.0.1");
        var response = await client.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        await AssertInvalidLicenceAsync(response);
    }

    [SkippableFact]
    public async Task Checkout_returns_409_no_seats_available_when_capacity_full()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync(maxSeats: 1);

        var firstResponse = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var body = await secondResponse.Content.ReadFromJsonAsync<NoSeatsAvailableResponse>();
        Assert.NotNull(body);
        Assert.Equal("no_seats_available", body!.Error);
        Assert.Equal(1, body.MaxSeats);
        Assert.Equal(1, body.ActiveSeats);
        Assert.True(body.OldestExpiresAt > DateTimeOffset.UtcNow);
    }

    [SkippableFact]
    public async Task Checkout_returns_same_seat_on_idempotent_replay()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync(maxSeats: 1);
        var instanceId = GenerateInstanceId();

        var first = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var firstJwt = await VerifySignedLicencePayloadAsync(firstPayload!.SignedPayload);
        var firstSeatId = firstJwt.Claims.Single(c => c.Type == "seatId").Value;

        var second = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondPayload = await second.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var secondJwt = await VerifySignedLicencePayloadAsync(secondPayload!.SignedPayload);
        var secondSeatId = secondJwt.Claims.Single(c => c.Type == "seatId").Value;

        Assert.Equal(firstSeatId, secondSeatId);
    }

    [SkippableFact]
    public async Task Checkin_returns_204_for_existing_seat_and_archives_history()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync();
        var openResponse = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        openResponse.EnsureSuccessStatusCode();
        var openPayload = await openResponse.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var openJwt = await VerifySignedLicencePayloadAsync(openPayload!.SignedPayload);
        var seatId = openJwt.Claims.Single(c => c.Type == "seatId").Value;

        var checkin = await UnauthedClient.DeleteAsync($"/licences/checkouts/{seatId}");
        Assert.Equal(HttpStatusCode.NoContent, checkin.StatusCode);

        await using var conn = await OpenDbAsync();
        var historyReason = await conn.QuerySingleAsync<string>(
            "SELECT close_reason FROM licence_checkout_history WHERE checkout_id = @Id::uuid;",
            new { Id = seatId });
        Assert.Equal("checkin", historyReason);
    }

    [SkippableFact]
    public async Task Checkin_returns_204_for_missing_seat()
    {
        var checkin = await UnauthedClient.DeleteAsync($"/licences/checkouts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NoContent, checkin.StatusCode);
    }

    [SkippableFact]
    public async Task Checkin_then_recheckout_yields_new_seat_id()
    {
        var (licenceKey, _, productId, _) = await CreateLicenceAsync(maxSeats: 1);
        var instanceId = GenerateInstanceId();

        var first = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var firstJwt = await VerifySignedLicencePayloadAsync(firstPayload!.SignedPayload);
        var firstSeatId = firstJwt.Claims.Single(c => c.Type == "seatId").Value;

        var checkin = await UnauthedClient.DeleteAsync($"/licences/checkouts/{firstSeatId}");
        Assert.Equal(HttpStatusCode.NoContent, checkin.StatusCode);

        var second = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey,
            productId,
            clientNonce = GenerateClientNonce(),
            instanceId
        });
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<SignedLicenceCheckoutResponse>();
        var secondJwt = await VerifySignedLicencePayloadAsync(secondPayload!.SignedPayload);
        var secondSeatId = secondJwt.Claims.Single(c => c.Type == "seatId").Value;

        Assert.NotEqual(firstSeatId, secondSeatId);
    }

    private static async Task AssertInvalidLicenceAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("invalid_licence", problem!.Title);
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    private async Task<(string LicenceKey, Guid LicenceId, Guid ProductId, Guid OwnerId)> CreateLicenceAsync(int maxSeats = 1, bool freshOwner = false)
    {
        var slug = $"checkout-{Guid.NewGuid():N}".Substring(0, 24);
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var ownerId = AdminUserId;
        if (freshOwner)
        {
            var email = $"checkout-owner-{Guid.NewGuid():N}@test.local";
            var userResponse = await AuthedClient.PostAsJsonAsync("/users", new { email, password = "checkout-owner-pw-12345", role = "user" });
            Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
            var user = await userResponse.Content.ReadFromJsonAsync<UserPayload>();
            Assert.NotNull(user);
            ownerId = user!.Id;
        }

        var licenceResponse = await AuthedClient.PostAsJsonAsync(
            "/licences",
            new { productId = product!.Id, userId = ownerId, maxSeats });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (licence!.LicenceKey, licence.Id, product.Id, ownerId);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record UserPayload(Guid Id, string Email);
}
