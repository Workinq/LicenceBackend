using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class ProductFilesEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Admin_upload_returns_201_with_version_1_metadata()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var product = await CreateProductAsync("upload-target", "Upload Target");
        var bytes = new byte[] { 0x42, 0x49, 0x4E };

        var response = await UploadFileAsync(product.Id, "release.bin", "application/octet-stream", bytes);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductFilePayload>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal(product.Id, body.ProductId);
        Assert.Equal(1, body.VersionNumber);
        Assert.Equal("release.bin", body.FileName);
        Assert.Equal("application/octet-stream", body.ContentType);
        Assert.Equal(bytes.Length, body.FileSizeBytes);
        Assert.Equal(AdminUserId, body.UploadedByAdminId);
        Assert.NotEqual(default, body.UploadedAt);
    }

    [SkippableFact]
    public async Task Second_upload_for_same_product_increments_version_to_2()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("versioned", "Versioned");

        var first = await UploadFileAsync(product.Id, "v1.bin", "application/octet-stream", new byte[] { 1 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var v1 = await first.Content.ReadFromJsonAsync<ProductFilePayload>();
        Assert.NotNull(v1);
        Assert.Equal(1, v1.VersionNumber);

        var second = await UploadFileAsync(product.Id, "v2.bin", "application/octet-stream", new byte[] { 2, 2 });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var v2 = await second.Content.ReadFromJsonAsync<ProductFilePayload>();
        Assert.NotNull(v2);
        Assert.Equal(2, v2.VersionNumber);
        Assert.NotEqual(v1.Id, v2.Id);
    }

    [SkippableFact]
    public async Task List_returns_all_versions_newest_first()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("listed", "Listed");

        await UploadFileAsync(product.Id, "a.bin", "application/octet-stream", new byte[] { 1 });
        await UploadFileAsync(product.Id, "b.bin", "application/octet-stream", new byte[] { 2 });
        await UploadFileAsync(product.Id, "c.bin", "application/octet-stream", new byte[] { 3 });

        var response = await AuthedClient.GetAsync($"/products/{product.Id}/files");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProductFilePayload>>();
        Assert.NotNull(list);
        Assert.Equal(3, list.Count);
        Assert.Equal(new[] { 3, 2, 1 }, list.Select(f => f.VersionNumber).ToArray());
        Assert.Equal(new[] { "c.bin", "b.bin", "a.bin" }, list.Select(f => f.FileName).ToArray());
    }

    [SkippableFact]
    public async Task List_for_unknown_product_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync($"/products/{Guid.NewGuid()}/files");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_can_download_specific_version_and_gets_bytes_back()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl", "DL");
        var payload = new byte[] { 10, 20, 30, 40, 50 };

        var upload = await UploadFileAsync(product.Id, "thing.bin", "application/zip", payload);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var meta = await upload.Content.ReadFromJsonAsync<ProductFilePayload>();
        Assert.NotNull(meta);

        var download = await AuthedClient.GetAsync($"/products/{product.Id}/files/{meta.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/zip", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("thing.bin", download.Content.Headers.ContentDisposition?.FileNameStar ?? download.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var bytes = await download.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.SequenceEqual(payload));
    }

    [SkippableFact]
    public async Task Empty_upload_returns_400()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("empty-up", "Empty");
        var response = await UploadFileAsync(product.Id, "empty.bin", "application/octet-stream", Array.Empty<byte>());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("product_file_empty", json);
    }

    [SkippableFact]
    public async Task Upload_to_unknown_product_returns_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await UploadFileAsync(Guid.NewGuid(), "x.bin", "application/octet-stream", new byte[] { 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_admin_cannot_upload()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("forbidden-up", "Forbidden");

        var userEmail = "uploader-denied@test.local";
        var userPassword = "uploader-denied-pw-12345";
        await AuthedClient.PostAsJsonAsync("/users", new { email = userEmail, password = userPassword });
        using var userClient = await CreateLoggedInClientAsync(userEmail, userPassword);

        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(new byte[] { 1 });
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", "denied.bin");
        var response = await userClient.PostAsync($"/products/{product.Id}/files", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task Upload_records_audit_event_with_admin_actor()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("audited-up", "Audited");

        var upload = await UploadFileAsync(product.Id, "tracked.bin", "application/octet-stream", new byte[] { 9, 9, 9 });
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        await using var conn = await OpenDbAsync();
        var count = (int)await Dapper.SqlMapper.ExecuteScalarAsync<long>(
            conn,
            "SELECT COUNT(*) FROM audit_events WHERE event_type = 'product.file_uploaded' AND subject_type = 'product' AND subject_id = @id AND actor_type = 'admin' AND actor_user_id = @aid",
            new { id = product.Id, aid = AdminUserId });
        Assert.Equal(1, count);
    }

    [SkippableFact]
    public async Task User_with_active_licence_downloads_latest_revision()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl-licence", "DL Licence");

        await UploadFileAsync(product.Id, "old.bin", "application/octet-stream", new byte[] { 1 });
        var newPayload = new byte[] { 7, 8, 9, 10 };
        await UploadFileAsync(product.Id, "current.bin", "application/zip", newPayload);

        var (client, licenceId) = await CreateLicensedUserAsync("dl-licence-customer", product.Id);

        var response = await client.GetAsync($"/me/licences/{licenceId}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.SequenceEqual(newPayload));
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.Equal("current.bin", disposition.FileNameStar ?? disposition.FileName?.Trim('"'));
    }

    [SkippableFact]
    public async Task User_without_licence_for_product_gets_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl-no-licence", "DL No Licence");
        await UploadFileAsync(product.Id, "x.bin", "application/octet-stream", new byte[] { 1 });

        var (client, _) = await CreateLicensedUserAsync("dl-no-licence-customer", product.Id);

        var response = await client.GetAsync($"/me/licences/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task User_with_revoked_licence_gets_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl-revoked", "DL Revoked");
        await UploadFileAsync(product.Id, "x.bin", "application/octet-stream", new byte[] { 1 });

        var (client, licenceId) = await CreateLicensedUserAsync("dl-revoked-customer", product.Id);
        var revoke = await AuthedClient.PatchAsJsonAsync($"/licences/{licenceId}/status", new { status = "revoked", reason = "test" });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var response = await client.GetAsync($"/me/licences/{licenceId}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task User_for_product_without_any_file_gets_404()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl-nofile", "DL Nofile");
        var (client, licenceId) = await CreateLicensedUserAsync("dl-nofile-customer", product.Id);

        var response = await client.GetAsync($"/me/licences/{licenceId}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task User_download_records_audit_event_with_user_actor()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var product = await CreateProductAsync("dl-audit", "DL Audit");
        await UploadFileAsync(product.Id, "x.bin", "application/octet-stream", new byte[] { 1, 2 });

        var (client, licenceId) = await CreateLicensedUserAsync("dl-audit-customer", product.Id);
        var response = await client.GetAsync($"/me/licences/{licenceId}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await response.Content.ReadAsByteArrayAsync();

        await using var conn = await OpenDbAsync();
        var count = (int)await Dapper.SqlMapper.ExecuteScalarAsync<long>(
            conn,
            "SELECT COUNT(*) FROM audit_events WHERE event_type = 'product.file_downloaded' AND subject_type = 'product' AND subject_id = @id AND actor_type = 'user'",
            new { id = product.Id });
        Assert.Equal(1, count);
    }

    private async Task<(HttpClient Client, Guid LicenceId)> CreateLicensedUserAsync(string slug, Guid productId)
    {
        var email = $"{slug}@test.local";
        var password = $"{slug}-pw-12345";
        var createUser = await AuthedClient.PostAsJsonAsync("/users", new { email, password });
        Assert.Equal(HttpStatusCode.Created, createUser.StatusCode);
        var createLicence = await AuthedClient.PostAsJsonAsync("/licences", new { productId, email });
        Assert.Equal(HttpStatusCode.Created, createLicence.StatusCode);
        var licence = await createLicence.Content.ReadFromJsonAsync<LicencePayload>();
        Assert.NotNull(licence);

        var client = await CreateLoggedInClientAsync(email, password);
        return (client, licence.Id);
    }

    private sealed record LicencePayload(Guid Id);

    private async Task<HttpResponseMessage> UploadFileAsync(Guid productId, string fileName, string contentType, byte[] bytes)
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(part, "file", fileName);
        return await AuthedClient.PostAsync($"/products/{productId}/files", content);
    }

    private async Task<ProductPayload> CreateProductAsync(string slug, string name)
    {
        var response = await AuthedClient.PostAsJsonAsync("/products", new { slug, displayName = name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProductPayload>();
        Assert.NotNull(body);
        return body;
    }

    private sealed record ProductPayload(Guid Id, string Slug, string DisplayName);

    private sealed record ProductFilePayload(
        Guid Id,
        Guid ProductId,
        int VersionNumber,
        string FileName,
        string ContentType,
        long FileSizeBytes,
        Guid UploadedByAdminId,
        DateTimeOffset UploadedAt
    );
}
