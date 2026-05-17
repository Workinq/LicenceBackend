using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class AuthorizationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Missing_authorization_header_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await UnauthedClient.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Garbage_bearer_value_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_bearer_scheme_returns_401()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        using var client = Factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "something");

        var response = await client.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Admin_jwt_can_access_admin_endpoints()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await AuthedClient.GetAsync("/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [SkippableFact]
    public async Task Non_admin_user_gets_403_on_admin_endpoints()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var createUserResponse = await AuthedClient.PostAsJsonAsync("/users", new { email = "regular@test.local", password = "regular-user-pw-12345", role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        using var regularClient = await CreateLoggedInClientAsync("regular@test.local", "regular-user-pw-12345");

        var productsResponse = await regularClient.PostAsJsonAsync("/products", new { slug = "denied-by-regular", displayName = "Denied" });
        Assert.Equal(HttpStatusCode.Forbidden, productsResponse.StatusCode);

        var licencesResponse = await regularClient.GetAsync("/licences");
        Assert.Equal(HttpStatusCode.Forbidden, licencesResponse.StatusCode);

        var usersResponse = await regularClient.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, usersResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Regular_user_can_access_me_endpoint()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var createUserResponse = await AuthedClient.PostAsJsonAsync("/users", new { email = "me-test@test.local", password = "me-test-pw-12345678", role = "user" });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        using var regularClient = await CreateLoggedInClientAsync("me-test@test.local", "me-test-pw-12345678");

        var meResponse = await regularClient.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Verify_endpoint_does_not_require_admin_auth()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");
        var response = await UnauthedClient.PostAsJsonAsync("/licences/verify", new { licenceKey = "LIC-NOT-A-REAL-KEY-AT-ALL", productId = Guid.NewGuid(), clientNonce = GenerateClientNonce() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
