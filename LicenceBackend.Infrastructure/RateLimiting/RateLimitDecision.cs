namespace LicenceBackend.Infrastructure.RateLimiting;

public readonly record struct RateLimitDecision(bool Acquired, TimeSpan? RetryAfter)
{
    public static RateLimitDecision Allow => new(true, null);
}
