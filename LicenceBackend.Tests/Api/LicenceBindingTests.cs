using System.Net;
using System.Net.Http.Json;
using Dapper;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceBindingTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task First_use_pin_sets_hwid_and_records_binding_history()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("bind-pin");

        var response = await Factory!.CreateClient().PostAsJsonAsync(
                           "/licences/verify",
                           new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var conn = await OpenDbAsync();
        var row = await conn.QuerySingleAsync<(byte[]? hwid_hmac, int history_count)>(
                      """
                      SELECT l.hwid_hmac,
                             (SELECT COUNT(*) FROM licence_binding_history
                              WHERE licence_id = l.id AND binding_type = 'hwid' AND change_source = 'first_use')::int AS history_count
                      FROM licences l WHERE l.id = @Id;
                      """,
                      new { Id = licenceId });
        Assert.NotNull(row.hwid_hmac);
        Assert.Equal(1, row.history_count);
    }

    [SkippableFact]
    public async Task Repeat_verify_with_same_hwid_succeeds()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("bind-repeat");
        var client = Factory!.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync(
                               "/licences/verify",
                               new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [SkippableFact]
    public async Task Verify_with_different_hwid_after_pin_fails_vague()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("bind-mismatch");
        var client = Factory!.CreateClient();

        var ok = await client.PostAsJsonAsync(
                     "/licences/verify",
                     new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var bad = await client.PostAsJsonAsync(
                      "/licences/verify",
                      new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-B" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var body = await bad.Content.ReadAsStringAsync();
        Assert.Contains("invalid_licence", body);
    }

    [SkippableFact]
    public async Task Verify_with_no_hwid_after_pin_fails_vague()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, _, licenceKey) = await CreateProductAndLicenceAsync("bind-missing");
        var client = Factory!.CreateClient();

        await client.PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });

        var missing = await client.PostAsJsonAsync(
                          "/licences/verify",
                          new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_clear_hwid_allows_re_pinning()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("bind-reset");

        await Factory!.CreateClient().PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });

        var clear = await AuthedClient.PutAsJsonAsync(
                        $"/licences/{licenceId}/hwid",
                        new { hwid = (string?)null, reason = "new machine" });
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var repin = await Factory!.CreateClient().PostAsJsonAsync(
                        "/licences/verify",
                        new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-B" });
        Assert.Equal(HttpStatusCode.OK, repin.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_cannot_set_specific_hwid()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, licenceId, _) = await CreateProductAndLicenceAsync("bind-set");

        var response = await AuthedClient.PutAsJsonAsync(
                           $"/licences/{licenceId}/hwid",
                           new { hwid = "device-pre-set", reason = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Ip_allowlist_allows_loopback_v4()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-allow-v4");

        var set = await AuthedClient.PutAsJsonAsync(
                      $"/licences/{licenceId}/ip-allowlist",
                      new { cidrs = new[] { "127.0.0.1/32" } });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var ok = await ClientFromIp("127.0.0.1").PostAsJsonAsync(
                     "/licences/verify",
                     new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [SkippableFact]
    public async Task Ip_allowlist_denies_outside_range()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-deny");

        var set = await AuthedClient.PutAsJsonAsync(
                      $"/licences/{licenceId}/ip-allowlist",
                      new { cidrs = new[] { "10.0.0.0/24" } });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var deny = await ClientFromIp("203.0.113.9").PostAsJsonAsync(
                       "/licences/verify",
                       new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, deny.StatusCode);
    }

    [SkippableFact]
    public async Task Ip_allowlist_ipv6_loopback_allowed()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-v6");

        var set = await AuthedClient.PutAsJsonAsync(
                      $"/licences/{licenceId}/ip-allowlist",
                      new { cidrs = new[] { "::1/128" } });
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var ok = await ClientFromIp("::1").PostAsJsonAsync(
                     "/licences/verify",
                     new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [SkippableFact]
    public async Task Ip_allowlist_rejects_invalid_cidr()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, licenceId, _) = await CreateProductAndLicenceAsync("ip-invalid");

        var response = await AuthedClient.PutAsJsonAsync(
                           $"/licences/{licenceId}/ip-allowlist",
                           new { cidrs = new[] { "not-a-cidr" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Ip_allowlist_empty_array_arms_first_use_bind()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-arm");

        var arm = await AuthedClient.PutAsJsonAsync(
                      $"/licences/{licenceId}/ip-allowlist",
                      new { cidrs = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.NoContent, arm.StatusCode);

        var verify = await ClientFromIp("203.0.113.5").PostAsJsonAsync(
                         "/licences/verify",
                         new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        await using var conn = await OpenDbAsync();
        var row = await conn.QuerySingleAsync<(string? ip_allowlist, int first_use_count)>(
                      """
                      SELECT l.ip_allowlist::text,
                             (SELECT COUNT(*) FROM licence_binding_history
                              WHERE licence_id = l.id AND binding_type = 'ip_allowlist' AND change_source = 'first_use')::int
                      FROM licences l WHERE l.id = @Id;
                      """,
                      new { Id = licenceId });
        Assert.Contains("203.0.113.5/32", row.ip_allowlist);
        Assert.Equal(1, row.first_use_count);
    }

    [SkippableFact]
    public async Task First_use_ip_bind_then_other_ip_denied_same_ip_allowed()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-firstuse");

        await AuthedClient.PutAsJsonAsync(
            $"/licences/{licenceId}/ip-allowlist",
            new { cidrs = Array.Empty<string>() });

        var first = await ClientFromIp("203.0.113.10").PostAsJsonAsync(
                        "/licences/verify",
                        new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var other = await ClientFromIp("203.0.113.11").PostAsJsonAsync(
                        "/licences/verify",
                        new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);

        var again = await ClientFromIp("203.0.113.10").PostAsJsonAsync(
                        "/licences/verify",
                        new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [SkippableFact]
    public async Task Create_licence_with_armed_ip_allowlist_binds_first_ip()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug = "ip-create-armed", displayName = "ip-create-armed" });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var createResponse = await AuthedClient.PostAsJsonAsync(
                                 "/licences",
                                 new { productId = product.Id, userId = AdminUserId, ipAllowlist = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var licence = await createResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        var verify = await ClientFromIp("198.51.100.7").PostAsJsonAsync(
                         "/licences/verify",
                         new { licenceKey = licence.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        await using var conn = await OpenDbAsync();
        var row = await conn.QuerySingleAsync<(string? ip_allowlist, int first_use_count)>(
                      """
                      SELECT l.ip_allowlist::text,
                             (SELECT COUNT(*) FROM licence_binding_history
                              WHERE licence_id = l.id AND binding_type = 'ip_allowlist' AND change_source = 'first_use')::int
                      FROM licences l WHERE l.id = @Id;
                      """,
                      new { Id = licence.Id });
        Assert.Contains("198.51.100.7/32", row.ip_allowlist);
        Assert.Equal(1, row.first_use_count);
    }

    [SkippableFact]
    public async Task Create_licence_with_fixed_ip_allowlist_enforces_it()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug = "ip-create-fixed", displayName = "ip-create-fixed" });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var createResponse = await AuthedClient.PostAsJsonAsync(
                                 "/licences",
                                 new { productId = product.Id, userId = AdminUserId, ipAllowlist = new[] { "10.0.0.0/24" } });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var licence = await createResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        var denied = await ClientFromIp("203.0.113.9").PostAsJsonAsync(
                         "/licences/verify",
                         new { licenceKey = licence.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        var allowed = await ClientFromIp("10.0.0.5").PostAsJsonAsync(
                          "/licences/verify",
                          new { licenceKey = licence.LicenceKey, productId = product.Id, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [SkippableFact]
    public async Task Create_licence_rejects_invalid_cidr_in_ip_allowlist()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var productResponse = await AuthedClient.PostAsJsonAsync("/products", new { slug = "ip-create-bad", displayName = "ip-create-bad" });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var createResponse = await AuthedClient.PostAsJsonAsync(
                                 "/licences",
                                 new { productId = product.Id, userId = AdminUserId, ipAllowlist = new[] { "not-a-cidr" } });
        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        var body = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("invalid_ip_allowlist", body);
    }

    [SkippableFact]
    public async Task Concurrent_first_use_binds_exactly_one_ip()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-race");

        await AuthedClient.PutAsJsonAsync(
            $"/licences/{licenceId}/ip-allowlist",
            new { cidrs = Array.Empty<string>() });

        var a = ClientFromIp("203.0.113.20").PostAsJsonAsync(
                    "/licences/verify",
                    new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        var b = ClientFromIp("203.0.113.21").PostAsJsonAsync(
                    "/licences/verify",
                    new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        var results = await Task.WhenAll(a, b);

        var okCount = results.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, okCount);

        await using var conn = await OpenDbAsync();
        var allowlist = await conn.QuerySingleAsync<string>(
                            "SELECT ip_allowlist::text FROM licences WHERE id = @Id;",
                            new { Id = licenceId });
        Assert.True(allowlist == "[\"203.0.113.20/32\"]" || allowlist == "[\"203.0.113.21/32\"]", allowlist);
    }

    [SkippableFact]
    public async Task Ip_allowlist_rejects_oversized_array()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, licenceId, _) = await CreateProductAndLicenceAsync("ip-oversized");

        var oversized = Enumerable.Range(0, 257).Select(_ => "10.0.0.0/24").ToArray();
        var response = await AuthedClient.PutAsJsonAsync(
                           $"/licences/{licenceId}/ip-allowlist",
                           new { cidrs = oversized });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_ip_allowlist", json);
    }

    [SkippableFact]
    public async Task Ip_allowlist_null_unrestricts()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("ip-null");

        await AuthedClient.PutAsJsonAsync(
            $"/licences/{licenceId}/ip-allowlist",
            new { cidrs = new[] { "10.0.0.0/24" } });

        var clear = await AuthedClient.PutAsJsonAsync(
                        $"/licences/{licenceId}/ip-allowlist",
                        new { cidrs = (string[]?)null });
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);

        var ok = await ClientFromIp("203.0.113.9").PostAsJsonAsync(
                     "/licences/verify",
                     new { licenceKey, productId, clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    private static readonly string[] Value = ["127.0.0.1/32"];

    [SkippableFact]
    public async Task Binding_history_paginates_newest_first()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (productId, _, licenceId, licenceKey) = await CreateProductAndLicenceAsync("bind-hist");

        await Factory!.CreateClient().PostAsJsonAsync(
            "/licences/verify",
            new { licenceKey, productId, clientNonce = GenerateClientNonce(), hwid = "device-A" });

        await AuthedClient.PutAsJsonAsync(
            $"/licences/{licenceId}/hwid",
            new { hwid = (string?)null, reason = "reset" });

        await AuthedClient.PutAsJsonAsync(
            $"/licences/{licenceId}/ip-allowlist",
            new { cidrs = Value });

        var response = await AuthedClient.GetAsync($"/licences/{licenceId}/binding-history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedEnvelope<BindingHistoryRow>>();
        Assert.NotNull(page);
        Assert.Equal(3, page.Total);
        Assert.Equal("ip_allowlist", page.Items[0].BindingType);
        Assert.Equal("admin", page.Items[0].ChangeSource);
        Assert.Equal("hwid", page.Items[1].BindingType);
        Assert.Equal("admin", page.Items[1].ChangeSource);
        Assert.Equal("hwid", page.Items[2].BindingType);
        Assert.Equal("first_use", page.Items[2].ChangeSource);
    }

    [SkippableFact]
    public async Task Non_admin_cannot_put_binding_endpoints()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var (_, _, licenceId, _) = await CreateProductAndLicenceAsync("bind-forbid");

        const string userEmail = "regular-binding@test.local";
        const string userPassword = "regular-binding-pw!";
        var createResp = await AuthedClient.PostAsJsonAsync(
                             "/users",
                             new { email = userEmail, password = userPassword, role = "user" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        using var userClient = await CreateLoggedInClientAsync(userEmail, userPassword);

        var hwidResp = await userClient.PutAsJsonAsync(
                           $"/licences/{licenceId}/hwid",
                           new { hwid = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, hwidResp.StatusCode);

        var ipResp = await userClient.PutAsJsonAsync(
                         $"/licences/{licenceId}/ip-allowlist",
                         new { cidrs = (string[]?)null });
        Assert.Equal(HttpStatusCode.Forbidden, ipResp.StatusCode);
    }

    private async Task<(Guid productId, string productSlug, Guid licenceId, string licenceKey)>
        CreateProductAndLicenceAsync(string slug)
    {
        var productResponse = await AuthedClient.PostAsJsonAsync(
                                  "/products",
                                  new { slug, displayName = slug });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(product);

        var licenceResponse = await AuthedClient.PostAsJsonAsync("/licences", new { productId = product.Id, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, licenceResponse.StatusCode);
        var licence = await licenceResponse.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        return (product.Id, product.Slug, licence.Id, licence.LicenceKey);
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName, DateTimeOffset CreatedAt);

    private sealed record LicencePayload(Guid Id, Guid ProductId, string LicenceKey);

    private sealed record BindingHistoryRow(
        Guid Id,
        string BindingType,
        string ChangeSource,
        Guid? ChangedByUserId,
        DateTimeOffset ChangedAt,
        string? Reason
    );

    private sealed record PagedEnvelope<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
}
