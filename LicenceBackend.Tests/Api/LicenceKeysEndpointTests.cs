using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Dapper;
using Microsoft.IdentityModel.Tokens;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceKeysEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Admin_can_mint_list_and_revoke_keys()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("keys-admin");
        var created = await CreateLicenceAsync(product.Id);
        var firstKey = created.LicenceKey;

        var mint = await AuthedClient.PostAsJsonAsync(
            $"/licences/{created.Id}/keys",
            new { label = "second", reason = "rotation" });
        Assert.Equal(HttpStatusCode.Created, mint.StatusCode);
        var minted = await mint.Content.ReadFromJsonAsync<LicenceKeyMintedPayload>();
        Assert.NotNull(minted);
        Assert.False(string.IsNullOrWhiteSpace(minted!.LicenceKey));
        Assert.NotEqual(firstKey, minted.LicenceKey);
        var secondKey = minted.LicenceKey;
        var secondKeyId = minted.Key.Id;

        var listed = await AuthedClient.GetFromJsonAsync<LicenceKeysPayload>($"/licences/{created.Id}/keys");
        Assert.NotNull(listed);
        Assert.Equal(2, listed!.ActiveCount);
        Assert.Equal(5, listed.ActiveCap);
        Assert.Equal(2, listed.Keys.Count);

        var firstVerify = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = firstKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, firstVerify.StatusCode);
        var secondVerify = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = secondKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, secondVerify.StatusCode);

        var revoke = await AuthedClient.SendAsync(BuildRevokeRequest(created.Id, secondKeyId, reason: "leaked"));
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var revokedSecond = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = secondKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.NotEqual(HttpStatusCode.OK, revokedSecond.StatusCode);

        var stillOriginal = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = firstKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, stillOriginal.StatusCode);
    }

    [SkippableFact]
    public async Task Owner_can_mint_and_revoke_own_keys()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("keys-owner");
        var ownerEmail = "keys-owner@test.local";
        var ownerPassword = "keys-owner-pw-12345"; // gitleaks:allow
        var (ownerId, _) = await CreateUserAndLoginAsync(ownerEmail, ownerPassword);
        var licence = await CreateLicenceForUserAsync(product.Id, ownerId);

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);
        var mint = await ownerClient.PostAsJsonAsync(
            $"/licences/{licence.Id}/keys",
            new { label = "spare", reason = (string?)null });
        Assert.Equal(HttpStatusCode.Created, mint.StatusCode);
        var minted = await mint.Content.ReadFromJsonAsync<LicenceKeyMintedPayload>();
        Assert.NotNull(minted);

        var revoke = await ownerClient.SendAsync(BuildRevokeRequest(licence.Id, minted!.Key.Id, reason: null));
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
    }

    [SkippableFact]
    public async Task Member_can_list_but_not_mint()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("keys-member");
        var ownerEmail = "keys-mem-owner@test.local";
        var (ownerId, _) = await CreateUserAndLoginAsync(ownerEmail, "keys-mem-owner-pw-12345");
        var memberEmail = "keys-mem@test.local";
        var memberPassword = "keys-mem-pw-12345"; // gitleaks:allow
        await CreateUserAndLoginAsync(memberEmail, memberPassword);
        var licence = await CreateLicenceForUserAsync(product.Id, ownerId);

        var addMember = await AuthedClient.PostAsJsonAsync(
            $"/licences/{licence.Id}/members",
            new { email = memberEmail });
        Assert.Equal(HttpStatusCode.Created, addMember.StatusCode);

        using var memberClient = await CreateLoggedInClientAsync(memberEmail, memberPassword);

        var list = await memberClient.GetAsync($"/licences/{licence.Id}/keys");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var payload = await list.Content.ReadFromJsonAsync<LicenceKeysPayload>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.ActiveCount);

        var mint = await memberClient.PostAsJsonAsync(
            $"/licences/{licence.Id}/keys",
            new { label = "no", reason = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, mint.StatusCode);
    }

    [SkippableFact]
    public async Task Mint_rejects_after_cap()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("keys-cap");
        var licence = await CreateLicenceAsync(product.Id);

        for (var i = 0; i < 4; i++)
        {
            var mint = await AuthedClient.PostAsJsonAsync(
                $"/licences/{licence.Id}/keys",
                new { label = $"k{i}", reason = (string?)null });
            Assert.Equal(HttpStatusCode.Created, mint.StatusCode);
        }

        var overflow = await AuthedClient.PostAsJsonAsync(
            $"/licences/{licence.Id}/keys",
            new { label = "overflow", reason = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
        var body = await overflow.Content.ReadAsStringAsync();
        Assert.Contains("licence_key_cap_exceeded", body);
    }

    [SkippableFact]
    public async Task Revoking_a_key_cascades_to_live_checkouts_issued_with_it()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("keys-cascade");
        var licence = await CreateLicenceAsync(product.Id, maxSeats: 3);
        var firstKey = licence.LicenceKey;

        var mint = await AuthedClient.PostAsJsonAsync(
            $"/licences/{licence.Id}/keys",
            new { label = "second", reason = (string?)null });
        Assert.Equal(HttpStatusCode.Created, mint.StatusCode);
        var minted = await mint.Content.ReadFromJsonAsync<LicenceKeyMintedPayload>();
        Assert.NotNull(minted);
        var secondKey = minted!.LicenceKey;
        var secondKeyId = minted.Key.Id;

        var co1 = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey = firstKey,
            productId = product.Id,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        Assert.Equal(HttpStatusCode.OK, co1.StatusCode);

        var co2 = await UnauthedClient.PostAsJsonAsync("/licences/checkout", new
        {
            licenceKey = secondKey,
            productId = product.Id,
            clientNonce = GenerateClientNonce(),
            instanceId = GenerateInstanceId()
        });
        Assert.Equal(HttpStatusCode.OK, co2.StatusCode);

        var revoke = await AuthedClient.SendAsync(BuildRevokeRequest(licence.Id, secondKeyId, reason: "cascade test"));
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        await using var db = await OpenDbAsync();
        var liveCount = await db.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM licence_checkouts WHERE licence_id = @LicenceId",
            new { LicenceId = licence.Id });
        Assert.Equal(1, liveCount);

        var keyRevokedHistory = (await db.QueryAsync(
            """
            SELECT close_reason
            FROM licence_checkout_history
            WHERE licence_id = @LicenceId AND close_reason = 'key_revoked'
            """,
            new { LicenceId = licence.Id })).ToList();
        Assert.Single(keyRevokedHistory);
    }

    private static HttpRequestMessage BuildRevokeRequest(Guid licenceId, Guid keyId, string? reason)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/licences/{licenceId}/keys/{keyId}")
        {
            Content = JsonContent.Create(new { reason })
        };
        return request;
    }

    private async Task<ProductPayload> CreateProductAsync(string slugBase)
    {
        var slug = $"{slugBase}-{Guid.NewGuid():N}".Substring(0, 24);
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slugBase });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body!;
    }

    private async Task<LicenceCreatedPayload> CreateLicenceAsync(Guid productId, int? maxSeats = null)
    {
        object payload = maxSeats is null
            ? new { productId, userId = AdminUserId }
            : new { productId, userId = AdminUserId, maxSeats = maxSeats.Value };
        var response = await AuthedClient.PostAsJsonAsync("/licences", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        return body!;
    }

    private async Task<LicenceCreatedPayload> CreateLicenceForUserAsync(Guid productId, Guid userId)
    {
        var response = await AuthedClient.PostAsJsonAsync("/licences", new { productId, userId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        return body!;
    }

    private async Task<(Guid Id, string Email)> CreateUserAndLoginAsync(string email, string password)
    {
        var response = await AuthedClient.PostAsJsonAsync("/users", new { email, password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(body);
        return (body!.Id, body.Email);
    }

    private static string GenerateInstanceId()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Base64UrlEncoder.Encode(bytes);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicenceCreatedPayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, string LicenceKey);

    private sealed record UserPayload(Guid Id, string Email, string? DisplayName, string Role, string Status, DateTimeOffset CreatedAt);

    private sealed record LicenceKeyPayload(
        Guid Id,
        Guid LicenceId,
        string KeyPrefix,
        string? Label,
        Guid? CreatedByUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastSeenAt,
        DateTimeOffset? RevokedAt,
        Guid? RevokedByUserId,
        string? RevokeReason
    );

    private sealed record LicenceKeyMintedPayload(LicenceKeyPayload Key, string LicenceKey);

    private sealed record LicenceKeysPayload(int ActiveCount, int ActiveCap, IReadOnlyList<LicenceKeyPayload> Keys);
}
