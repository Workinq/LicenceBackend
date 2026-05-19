using LicenceBackend.Core.Auditing;
using LicenceBackend.Core.Auditing.Payloads;
using LicenceBackend.Core.Licences;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Infrastructure.Hosting;

public sealed class LicenceCheckoutSweeper(
    ILicenceCheckoutRepository checkouts,
    IAuditEventRepository auditEvents,
    IOptions<LicenceCheckoutOptions> options,
    TimeProvider time,
    ILogger<LicenceCheckoutSweeper> logger
) : BackgroundService
{
    private readonly LicenceCheckoutOptions _options = options.Value;

    public async Task<ReclaimExpiredResult> SweepOnceAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var result = await checkouts.ReclaimExpiredAsync(now, cancellationToken);

        if (result.ReclaimedCount > 0)
        {
            var evt = AuditEvent.Create(
                AuditEventTypes.LicenceCheckoutSweeperRan,
                AuditSubjectTypes.System,
                Guid.Empty,
                AuditActorTypes.System,
                actorUserId: null,
                reason: null,
                new LicenceCheckoutSweeperRanPayload(result.ReclaimedCount, result.LicencesAffected),
                now);
            await auditEvents.RecordAsync(evt, cancellationToken);
        }

        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.SweepIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LicenceCheckoutSweeper iteration failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
