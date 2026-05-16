using System.Text.Json;
using LicenceBackend.Api.Models.Response;
using LicenceBackend.Api.RateLimiting;
using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LicenceBackend.Api.Controllers;

[ApiController]
[Route("audit-events")]
[Authorize(Roles = "admin")]
[EnableRateLimiting(RateLimiterPolicyNames.Admin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public sealed class AuditEventsController(IAuditEventRepository auditEvents, IUserRepository users) : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery(Name = "subject_type")] string? subjectType,
        [FromQuery(Name = "subject_id")] Guid? subjectId,
        [FromQuery(Name = "event_type")] string? eventType,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken
    )
    {
        if (subjectType is not null && subjectType != AuditSubjectTypes.User && subjectType != AuditSubjectTypes.Licence)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: ProblemTitles.InvalidSubjectType,
                detail: $"subject_type must be '{AuditSubjectTypes.User}' or '{AuditSubjectTypes.Licence}'."
            );

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var effectiveOffset = Math.Max(offset ?? 0, 0);
        var page = await auditEvents.QueryAsync(subjectType, subjectId, eventType, effectiveLimit, effectiveOffset, cancellationToken);

        var emailByUserId = new Dictionary<Guid, string>();
        foreach (var actorUserId in page.Items.Select(e => e.ActorUserId).OfType<Guid>().Distinct())
        {
            var actor = await users.FindByIdAsync(actorUserId, cancellationToken);
            if (actor is not null) emailByUserId[actorUserId] = actor.Email;
        }

        var items = page.Items
                        .Select(evt => new AuditEventResponse(
                                    evt.Id,
                                    evt.OccurredAt,
                                    evt.EventType,
                                    evt.SubjectType,
                                    evt.SubjectId,
                                    evt.ActorType,
                                    evt.ActorUserId,
                                    evt.ActorUserId is { } id ? emailByUserId.GetValueOrDefault(id) : null,
                                    evt.Reason,
                                    JsonDocument.Parse(evt.PayloadJson).RootElement.Clone()
                                ))
                        .ToList();

        return Ok(new PagedResponse<AuditEventResponse>(items, page.Total, effectiveLimit, effectiveOffset));
    }
}
