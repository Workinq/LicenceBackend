namespace LicenceBackend.Infrastructure.RateLimiting;

public interface ILicenceCheckoutRateLimiter
{
    ValueTask<RateLimitDecision> TryAcquireAsync(string licenceKey, string instanceId, CancellationToken cancellationToken);
}
