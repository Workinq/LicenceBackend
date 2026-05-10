using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace LicenceBackend.Api.RateLimiting;

public static class RateLimitRejection
{
    private const string ProblemTitle = "rate_limited";
    private const string ProblemDetail = "Too many requests. Retry later.";

    public static async Task WriteAsync(HttpContext context, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        SetRetryAfter(context.Response, retryAfter);

        var problem = BuildProblem(retryAfter);
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: cancellationToken);
    }

    public static IActionResult AsResult(HttpContext context, TimeSpan? retryAfter)
    {
        SetRetryAfter(context.Response, retryAfter);
        var problem = BuildProblem(retryAfter);
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static ProblemDetails BuildProblem(TimeSpan? retryAfter)
    {
        return new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Title = ProblemTitle,
            Status = StatusCodes.Status429TooManyRequests,
            Detail = ProblemDetail,
            Extensions =
            {
                ["retryAfterSeconds"] = retryAfter.HasValue ? (int)Math.Ceiling(retryAfter.Value.TotalSeconds) : null
            }
        };
    }

    private static void SetRetryAfter(HttpResponse response, TimeSpan? retryAfter)
    {
        var source = retryAfter ?? TimeSpan.FromSeconds(60);
        var seconds = Math.Max(1, (int)Math.Ceiling(source.TotalSeconds));
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
    }
}
