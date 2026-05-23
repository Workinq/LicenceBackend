using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController(NpgsqlDataSource dataSource, ILogger<HealthController> logger) : ControllerBase
{
    private static readonly string Version = ResolveVersion();

    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return Ok(new HealthResponse("ok", "ok", Version));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check DB probe failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("degraded", ex.GetType().Name, Version));
        }
    }

    private static string ResolveVersion()
    {
        var info = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info)) return "0.0.0";
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }

    public sealed record HealthResponse(string Status, string Db, string Version);
}
