using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public sealed class HealthController(NpgsqlDataSource dataSource, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var command    = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            return Ok(new HealthResponse("ok", "ok"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Health check DB probe failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("degraded", ex.GetType().Name));
        }
    }

    public sealed record HealthResponse(string Status, string Db);
}
