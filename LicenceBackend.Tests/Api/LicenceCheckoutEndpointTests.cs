using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
        var payload = await response.Content.ReadFromJsonAsync<SignedCheckoutPayload>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrEmpty(payload!.SignedPayload));

        var jwt = await VerifySignedLicencePayloadAsync(payload.SignedPayload);
        Assert.Equal(licenceId.ToString(), jwt.Claims.Single(c => c.Type == "licenceId").Value);
        Assert.Equal(productId.ToString(), jwt.Claims.Single(c => c.Type == "productId").Value);
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatId"));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatExpiresAt"));
        Assert.NotNull(jwt.Claims.SingleOrDefault(c => c.Type == "seatHeartbeatAfter"));
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    private async Task<(string LicenceKey, Guid LicenceId, Guid ProductId, Guid OwnerId)> CreateLicenceAsync(int maxSeats = 1)
    {
        var slug = $"checkout-{Guid.NewGuid():N}".Substring(0, 24);
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync(
            "/licences",
            new { productId = product!.Id, userId = AdminUserId, maxSeats });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (licence!.LicenceKey, licence.Id, product.Id, AdminUserId);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record SignedCheckoutPayload(string SignedPayload);
}
