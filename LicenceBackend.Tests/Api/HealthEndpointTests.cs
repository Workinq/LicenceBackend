using System.Net;
using System.Net.Http.Json;

namespace LicenceBackend.Tests.Api;

public sealed class HealthEndpointTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task Health_returns_200_with_db_ok()
    {
        Skip.If(Factory is null, "Fixture was not initialised.");

        var response = await UnauthedClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("ok", body.Db);
    }

    private sealed record HealthPayload(string Status, string Db);
}
