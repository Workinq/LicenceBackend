using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Licences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("verification-attempts")]
[Authorize(Roles = "admin")]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class VerificationAttemptsController(ILicenceVerificationAttemptRepository verificationAttempts) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<VerificationAttemptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? outcome,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        if (!LicencesController.TryParseOutcomeFilter(outcome, out var filter))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "invalid_outcome",
                detail: "outcome must be 'approved' or 'denied'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await verificationAttempts.ListAsync(filter, effectiveLimit, effectiveOffset, cancellationToken);

        var items = page.Items.Select(LicencesController.ToVerificationAttemptResponse).ToList();
        return Ok(new PagedResponse<VerificationAttemptResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }
}
