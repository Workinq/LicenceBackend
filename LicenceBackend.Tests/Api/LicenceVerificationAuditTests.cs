using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceVerificationAuditTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Approved_verify_records_one_approved_row_with_hwid_and_ip()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-ok");

        var response = await ClientFromIp("127.0.0.1").PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-X" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var rows = (await conn.QueryAsync<(string outcome, string? denial_reason, string? hwid_hmac_base64, string source_ip)>(
                        """
                        SELECT (payload->>'outcome') AS outcome,
                               (payload->>'denialReason') AS denial_reason,
                               (payload->>'hwidHmacBase64') AS hwid_hmac_base64,
                               (payload->>'sourceIp') AS source_ip
                        FROM audit_events
                        WHERE event_type = 'licence.verified' AND subject_id = @Id
                        """,
                        new { Id = licenceId })).ToList();
        Assert.Single(rows);
        Assert.Equal("approved", rows[0].outcome);
        Assert.Null(rows[0].denial_reason);
        Assert.NotNull(rows[0].hwid_hmac_base64);
        Assert.Equal("127.0.0.1", rows[0].source_ip);
    }

    [SkippableFact]
    public async Task Product_mismatch_records_denial()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-product");
        var response = await Factory!.CreateClient().PostAsJsonAsync("/licences/verify", new { licenceKey, productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDenialReasonAsync(licenceId, "product_mismatch");
    }

    [SkippableFact]
    public async Task Licence_not_usable_records_denial_when_revoked()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-revoked");
        var patch = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "revoked", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var response = await Factory!.CreateClient().PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDenialReasonAsync(licenceId, "licence_not_usable");
    }

    private static readonly string[] Value = ["10.0.0.0/24"];

    [SkippableFact]
    public async Task Ip_not_allowlisted_records_denial()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-ip");
        await AuthedClient.PutAsJsonAsync($"/licences/{licenceId}/ip-allowlist", new { cidrs = Value });

        var response = await ClientFromIp("203.0.113.9").PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertDenialReasonAsync(licenceId, "ip_not_allowlisted");
    }

    [SkippableFact]
    public async Task Hwid_first_pin_and_audit_row_are_durably_joined()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-pin-join");
        var response = await ClientFromIp("127.0.0.1").PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-pin" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var pinned = await conn.QuerySingleAsync<(byte[]? hwid_hmac, short? hwid_hmac_pepper_version)>(
                         "SELECT hwid_hmac, hwid_hmac_pepper_version FROM licences WHERE id = @Id",
                         new { Id = licenceId });
        Assert.NotNull(pinned.hwid_hmac);
        Assert.Equal((short)1, pinned.hwid_hmac_pepper_version);

        var attempts = (await conn.QueryAsync<(string outcome, string? hwid_hmac_base64)>(
                            """
                            SELECT (payload->>'outcome') AS outcome,
                                   (payload->>'hwidHmacBase64') AS hwid_hmac_base64
                            FROM audit_events
                            WHERE event_type = 'licence.verified' AND subject_id = @Id
                            """,
                            new { Id = licenceId })).ToList();
        Assert.Single(attempts);
        Assert.Equal("approved", attempts[0].outcome);
        Assert.NotNull(attempts[0].hwid_hmac_base64);
        Assert.Equal(pinned.hwid_hmac, Convert.FromBase64String(attempts[0].hwid_hmac_base64!));
    }

    [SkippableFact]
    public async Task Hwid_missing_records_denial_after_pin()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-hwid-missing");
        var client = Factory!.CreateClient();
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });

        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertContainsDenialReasonAsync(licenceId, "hwid_missing");
    }

    [SkippableFact]
    public async Task Hwid_mismatch_records_denial()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-hwid-mismatch");
        var client = Factory!.CreateClient();
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });

        var response = await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-B" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await AssertContainsDenialReasonAsync(licenceId, "hwid_mismatch");
    }

    [SkippableFact]
    public async Task Unknown_licence_key_records_no_audit_row()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, _) = await CreateProductAndLicenceAsync("audit-unknown");
        await using var conn = await OpenDbAsync();
        var before = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM audit_events WHERE event_type = 'licence.verified'");

        var response = await Factory!.CreateClient().PostAsJsonAsync("/licences/verify", new { licenceKey = "LIC-ABCDE-FGHJK-MNPQR-STVWX-YZ234", productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM audit_events WHERE event_type = 'licence.verified'");
        Assert.Equal(before, after);
    }

    [SkippableFact]
    public async Task Admin_per_licence_endpoint_returns_all_rows_and_filters_by_outcome()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("audit-admin-list");
        var client = Factory!.CreateClient();

        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-B" });

        var all = await AuthedClient.GetFromJsonAsync<PagedEnvelope<AttemptRow>>($"/licences/{licenceId}/verification-attempts");
        Assert.NotNull(all);
        Assert.Equal(2, all.Total);
        Assert.Equal("denied", all.Items[0].Outcome);
        Assert.Equal("approved", all.Items[1].Outcome);

        var denied = await AuthedClient.GetFromJsonAsync<PagedEnvelope<AttemptRow>>($"/licences/{licenceId}/verification-attempts?outcome=denied");
        Assert.NotNull(denied);
        Assert.Equal(1, denied.Total);
        Assert.Equal("denied", denied.Items[0].Outcome);
        Assert.Equal("hwid_mismatch", denied.Items[0].DenialReason);
    }

    [SkippableFact]
    public async Task Owner_endpoint_returns_only_approved_even_when_denials_exist()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string ownerEmail = "audit-owner@test.local";
        const string ownerPassword = "audit-owner-pw!";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = ownerEmail, password = ownerPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        var ownerUser = await createUser.Content.ReadFromJsonAsync<UserPayload>();
        Assert.NotNull(ownerUser);

        var (productId, _, licenceId, licenceKey) = await CreateLicenceForOwnerAsync("audit-owner", ownerUser.Id);
        var client = Factory!.CreateClient();

        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-B" });

        using var ownerClient = await CreateLoggedInClientAsync(ownerEmail, ownerPassword);
        var page = await ownerClient.GetFromJsonAsync<PagedEnvelope<AttemptRow>>($"/me/licences/{licenceId}/verification-attempts");
        Assert.NotNull(page);
        Assert.Equal(1, page.Total);
        Assert.Equal("approved", page.Items[0].Outcome);
    }

    [SkippableFact]
    public async Task Owner_cannot_see_other_users_licence_attempts()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string otherEmail = "audit-other@test.local";
        const string otherPassword = "audit-other-pw!";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = otherEmail, password = otherPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        var (_, _, licenceId, _) = await CreateProductAndLicenceAsync("audit-foreign");

        using var otherClient = await CreateLoggedInClientAsync(otherEmail, otherPassword);
        var response = await otherClient.GetAsync($"/me/licences/{licenceId}/verification-attempts");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_cross_licence_denials_feed_returns_multiple_licences()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productA, _, _, licenceKeyA) = await CreateProductAndLicenceAsync("audit-cross-a");
        var (_, _, _, licenceKeyB) = await CreateProductAndLicenceAsync("audit-cross-b");
        var client = Factory!.CreateClient();

        await client.PostAsJsonAsync("/licences/verify", new { licenceKey = licenceKeyA, productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey = licenceKeyB, productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        await client.PostAsJsonAsync("/licences/verify", new { licenceKey = licenceKeyA, productId = productA, clientNonce = GenerateClientNonce() });

        var feed = await AuthedClient.GetFromJsonAsync<PagedEnvelope<AttemptRow>>("/verification-attempts?outcome=denied");
        Assert.NotNull(feed);
        Assert.True(feed.Total >= 2);
        Assert.DoesNotContain(feed.Items, r => r.Outcome == "approved");
        var licenceIds = feed.Items.Select(r => r.LicenceId).Distinct().ToList();
        Assert.True(licenceIds.Count >= 2);
    }

    [SkippableFact]
    public async Task Non_admin_cross_licence_feed_returns_403()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        const string userEmail = "audit-noadmin@test.local";
        const string userPassword = "audit-noadmin-pw!";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email = userEmail, password = userPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);

        using var userClient = await CreateLoggedInClientAsync(userEmail, userPassword);
        var response = await userClient.GetAsync("/verification-attempts");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task AssertDenialReasonAsync(Guid licenceId, string expectedReason)
    {
        await using var conn = await OpenDbAsync();
        var rows = (await conn.QueryAsync<(string outcome, string? denial_reason)>(
                        """
                        SELECT (payload->>'outcome') AS outcome,
                               (payload->>'denialReason') AS denial_reason
                        FROM audit_events
                        WHERE event_type = 'licence.verified' AND subject_id = @Id
                        """,
                        new { Id = licenceId })).ToList();
        Assert.Single(rows);
        Assert.Equal("denied", rows[0].outcome);
        Assert.Equal(expectedReason, rows[0].denial_reason);
    }

    private async Task AssertContainsDenialReasonAsync(Guid licenceId, string expectedReason)
    {
        await using var conn = await OpenDbAsync();
        var rows = (await conn.QueryAsync<(string outcome, string? denial_reason)>(
                        """
                        SELECT (payload->>'outcome') AS outcome,
                               (payload->>'denialReason') AS denial_reason
                        FROM audit_events
                        WHERE event_type = 'licence.verified' AND subject_id = @Id
                        ORDER BY occurred_at DESC
                        """,
                        new { Id = licenceId })).ToList();
        Assert.Contains(rows, r => r.outcome == "denied" && r.denial_reason == expectedReason);
    }

    private async Task<(Guid productId, string productSlug, Guid licenceId, string licenceKey)>
        CreateProductAndLicenceAsync(string slug)
    {
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (product.Id, product.Slug, licence.Id, licence.LicenceKey);
    }

    private async Task<(Guid productId, string productSlug, Guid licenceId, string licenceKey)>
        CreateLicenceForOwnerAsync(string slug, Guid ownerId)
    {
        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = ownerId });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (product.Id, product.Slug, licence.Id, licence.LicenceKey);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record UserPayload(Guid Id, string Email);

    private sealed record AttemptRow(
        Guid Id,
        Guid LicenceId,
        Guid? ProductIdRequested,
        string? HwidFingerprint,
        string SourceIp,
        string Outcome,
        string? DenialReason,
        DateTimeOffset AttemptedAt
    );

    private sealed record PagedEnvelope<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
}
