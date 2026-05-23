using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceKeyRegenerationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Regenerate_returns_new_key_invalidates_old_key_and_keeps_other_fields()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-ok", "Regen OK");
        var created = await CreateLicenceAsync(product.Id);
        var oldKey = created.LicenceKey;

        // sanity: old key verifies before regeneration
        var beforeVerify = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify", new { licenceKey = oldKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, beforeVerify.StatusCode);

        var response = await AuthedClient.PostAsJsonAsync(
            $"/licences/{created.Id}/regenerate-key", new { reason = "leaked key" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LicenceKeyRegeneratedPayload>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body.Id);
        Assert.Equal(product.Id, body.ProductId);
        Assert.Equal("regen-ok", body.ProductSlug);
        Assert.Equal(AdminUserId, body.UserId);
        Assert.Equal("active", body.Status);
        Assert.False(string.IsNullOrWhiteSpace(body.LicenceKey));
        Assert.StartsWith("LIC-", body.LicenceKey);
        Assert.NotEqual(oldKey, body.LicenceKey);

        // old key no longer verifies
        var afterOld = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify", new { licenceKey = oldKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.NotEqual(HttpStatusCode.OK, afterOld.StatusCode);

        // new key verifies
        var afterNew = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify", new { licenceKey = body.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, afterNew.StatusCode);
    }

    [SkippableFact]
    public async Task Regenerate_writes_one_audit_row_with_changer_and_reason()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-audit", "Regen Audit");
        var created = await CreateLicenceAsync(product.Id);

        var response = await AuthedClient.PostAsJsonAsync(
            $"/licences/{created.Id}/regenerate-key", new { reason = "  rotating  " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = await OpenDbAsync();
        var rows = (await db.QueryAsync(
            """
            SELECT subject_id, actor_user_id, reason,
                   (payload->>'previousKeyHmacBase64') AS previous_key_hmac_base64,
                   (payload->>'newKeyHmacBase64') AS new_key_hmac_base64
            FROM audit_events
            WHERE event_type = 'licence.key_regenerated' AND subject_id = @Id
            """,
            new { Id = created.Id })).ToList();

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(created.Id, (Guid)row.subject_id);
        Assert.Equal(AdminUserId, (Guid)row.actor_user_id);
        Assert.Equal("rotating", (string)row.reason); // trimmed
        var previousKeyHmac = Convert.FromBase64String((string)row.previous_key_hmac_base64);
        var newKeyHmac = Convert.FromBase64String((string)row.new_key_hmac_base64);
        Assert.NotNull(previousKeyHmac);
        Assert.NotNull(newKeyHmac);
        Assert.False(previousKeyHmac.SequenceEqual(newKeyHmac));
    }

    [SkippableFact]
    public async Task Regenerate_with_blank_reason_stores_null()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-blank", "Regen Blank");
        var created = await CreateLicenceAsync(product.Id);

        var response = await AuthedClient.PostAsJsonAsync(
            $"/licences/{created.Id}/regenerate-key", new { reason = "   " });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var db = await OpenDbAsync();
        var reason = await db.ExecuteScalarAsync<string?>(
            "SELECT reason FROM audit_events WHERE event_type = 'licence.key_regenerated' AND subject_id = @Id",
            new { Id = created.Id });
        Assert.Null(reason);
    }

    [SkippableFact]
    public async Task Regenerate_unknown_licence_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await AuthedClient.PostAsJsonAsync(
            $"/licences/{Guid.NewGuid()}/regenerate-key", new { reason = (string?)null });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("licence_not_found", json);
    }

    [SkippableFact]
    public async Task Regenerate_revokes_all_active_keys_and_only_the_new_one_verifies()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-multi", "Regen Multi");
        var created = await CreateLicenceAsync(product.Id);

        var extraResp = await AuthedClient.PostAsJsonAsync($"/licences/{created.Id}/keys", new { label = "extra", reason = (string?)null });
        extraResp.EnsureSuccessStatusCode();
        var extra = await extraResp.Content.ReadFromJsonAsync<MintedKeyPayload>();
        Assert.NotNull(extra);

        var regen = await AuthedClient.PostAsJsonAsync($"/licences/{created.Id}/regenerate-key", new { reason = "rotation" });
        Assert.Equal(HttpStatusCode.OK, regen.StatusCode);
        var newBody = await regen.Content.ReadFromJsonAsync<LicenceKeyRegeneratedPayload>();
        Assert.NotNull(newBody);

        foreach (var old in new[] { created.LicenceKey, extra!.LicenceKey })
        {
            var resp = await UnauthedClient.PostAsJsonAsync(
                "/licences/verify",
                new { licenceKey = old, productId = product.Id, clientNonce = GenerateClientNonce() });
            Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        }

        var ok = await UnauthedClient.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey = newBody!.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    private sealed record MintedKeyPayload(KeyDto Key, string LicenceKey);
    private sealed record KeyDto(Guid Id, Guid LicenceId, string KeyPrefix, string? Label, Guid? CreatedByUserId, DateTimeOffset CreatedAt, DateTimeOffset? LastSeenAt, DateTimeOffset? RevokedAt, Guid? RevokedByUserId, string? RevokeReason);

    [SkippableFact]
    public async Task Regenerate_as_non_admin_is_forbidden()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("regen-forbidden", "Regen Forbidden");
        var created = await CreateLicenceAsync(product.Id);

        var regularEmail = "regular-regen@test.local";
        var regularPassword = "regular-regen-pw-123!";
        await using (var db = await OpenDbAsync())
        {
            await db.ExecuteAsync(
                """
                INSERT INTO users (id, email, email_lower, password_hash, display_name, role, status, created_at, updated_at)
                VALUES (@Id, @Email, @EmailLower, @Hash, NULL, 'user', 'active', NOW(), NOW());
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    Email = regularEmail,
                    EmailLower = regularEmail,
                    Hash = new LicenceBackend.Infrastructure.Crypto.Argon2IdPasswordHasher().Hash(regularPassword)
                });
        }
        using var regularClient = await CreateLoggedInClientAsync(regularEmail, regularPassword);

        var response = await regularClient.PostAsJsonAsync(
            $"/licences/{created.Id}/regenerate-key", new { reason = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductPayload>())!;
    }

    private async Task<LicenceCreatedPayload> CreateLicenceAsync(Guid productId)
    {
        var response = await AuthedClient.PostAsJsonAsync("/licences", new { productId, userId = AdminUserId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LicenceCreatedPayload>())!;
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicenceCreatedPayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, string LicenceKey);

    private sealed record LicenceKeyRegeneratedPayload(Guid Id, Guid ProductId, string ProductSlug, Guid UserId, string UserEmail, string Status, string LicenceKey);
}
