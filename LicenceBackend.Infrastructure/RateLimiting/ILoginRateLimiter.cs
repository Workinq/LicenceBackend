namespace LicenceBackend.Infrastructure.RateLimiting;

public interface ILoginRateLimiter
{
    ValueTask<RateLimitDecision> TryAcquireAsync(string ip, string email, CancellationToken cancellationToken);
}
