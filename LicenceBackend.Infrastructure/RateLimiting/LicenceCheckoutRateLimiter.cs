using System.Threading.RateLimiting;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Infrastructure.RateLimiting;

public sealed class LicenceCheckoutRateLimiter : ILicenceCheckoutRateLimiter, IDisposable
{
    private readonly bool _enabled;
    private readonly PartitionedRateLimiter<string>? _limiter;

    public LicenceCheckoutRateLimiter(IOptions<RateLimitingOptions> options)
    {
        var opts = options.Value;
        _enabled = opts.Enabled;
        if (!_enabled) return;

        _limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = opts.Checkout.PermitLimit,
                Window = TimeSpan.FromSeconds(opts.Checkout.WindowSeconds),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
    }

    public ValueTask<RateLimitDecision> TryAcquireAsync(string licenceKey, string instanceId, CancellationToken cancellationToken)
    {
        if (!_enabled || _limiter is null) return ValueTask.FromResult(RateLimitDecision.Allow);

        var key = $"{licenceKey}|{instanceId}";
        using var lease = _limiter.AttemptAcquire(key);
        if (lease.IsAcquired) return ValueTask.FromResult(RateLimitDecision.Allow);

        TimeSpan? retryAfter = null;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var metadata)) retryAfter = metadata;
        return ValueTask.FromResult(new RateLimitDecision(false, retryAfter));
    }

    public void Dispose() => _limiter?.Dispose();
}
