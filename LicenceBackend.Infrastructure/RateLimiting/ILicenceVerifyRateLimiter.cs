namespace LicenceBackend.Infrastructure.RateLimiting;

public interface ILicenceVerifyRateLimiter
{
    ValueTask<RateLimitDecision> TryAcquireAsync(string licenceKey, CancellationToken cancellationToken);
}
