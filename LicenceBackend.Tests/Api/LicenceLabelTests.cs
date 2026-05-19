using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class LicenceLabelTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Patch_label_as_owner_updates_label()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-edit", "Label Edit");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        var resp = await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "Renamed" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LabelPayload>();
        Assert.NotNull(body);
        Assert.Equal("Renamed", body.Label);
    }

    [SkippableFact]
    public async Task Patch_label_with_null_clears_label()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-clear", "Label Clear");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "Initial" });
        var resp = await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = (string?)null });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LabelPayload>();
        Assert.NotNull(body);
        Assert.Null(body.Label);
    }

    [SkippableFact]
    public async Task Patch_label_with_blank_string_clears_label()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-blank", "Label Blank");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "Something" });
        var resp = await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "   " });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LabelPayload>();
        Assert.NotNull(body);
        Assert.Null(body.Label);
    }

    [SkippableFact]
    public async Task Patch_label_too_long_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-too-long", "Label Too Long");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        var resp = await AuthedClient.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = new string('x', 11) });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("label_too_long", json);
    }

    [SkippableFact]
    public async Task Patch_label_as_non_owner_returns_403()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-foreign", "Label Foreign");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        var email = "label-other@test.local";
        var password = "label-other-pw-12345";
        await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        using var other = await CreateLoggedInClientAsync(email, password);

        var resp = await other.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "intruder" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_label_as_member_returns_403()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("label-member", "Label Member");
        var licenceId = await CreateLicenceForAdminAsync(product.Id);

        var email = "label-member-user@test.local";
        var password = "label-member-pw-12345";
        await AuthedClient.PostAsJsonAsync("/users", new { email, password, role = "user" });
        var addMember = await AuthedClient.PostAsJsonAsync($"/licences/{licenceId}/members", new { email });
        Assert.Equal(HttpStatusCode.Created, addMember.StatusCode);

        using var member = await CreateLoggedInClientAsync(email, password);
        var resp = await member.PatchAsJsonAsync($"/me/licences/{licenceId}/label", new { label = "member-set" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [SkippableFact]
    public async Task Patch_label_on_unknown_licence_returns_403()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var resp = await AuthedClient.PatchAsJsonAsync($"/me/licences/{Guid.NewGuid()}/label", new { label = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<Guid> CreateLicenceForAdminAsync(Guid productId)
    {
        var resp = await AuthedClient.PostAsJsonAsync("/licences", new { productId, userId = AdminUserId });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<LicenceCreatedPayload>();
        Assert.NotNull(body);
        return body.Id;
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName);
    private sealed record LicenceCreatedPayload(Guid Id);
    private sealed record LabelPayload(Guid Id, string? Label);
}
