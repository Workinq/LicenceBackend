using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Licences;

public sealed record OpenCheckoutResult(
    LicenceCheckout Checkout,
    int SeatsAfter,
    int MaxSeats,
    bool IsIdempotentReplay
);

public sealed record DeniedNoSeatsResult(
    int ActiveSeats,
    int MaxSeats,
    DateTimeOffset OldestExpiresAt
);

public abstract record OpenCheckoutOutcome
{
    public sealed record Opened(OpenCheckoutResult Result) : OpenCheckoutOutcome;
    public sealed record DeniedNoSeats(DeniedNoSeatsResult Detail) : OpenCheckoutOutcome;
    public sealed record LicenceNotFound : OpenCheckoutOutcome;
}

public sealed record ReclaimExpiredResult(int ReclaimedCount, int LicencesAffected);

public sealed record OpenCheckoutParameters(
    Guid LicenceId,
    byte[] InstanceIdHash,
    Guid? MemberUserId,
    byte[]? HwidHmac,
    short? HwidHmacPepperVersion,
    string SourceIp,
    Guid? IssuedWithLicenceKeyId,
    TimeSpan LeaseDuration
);

public interface ILicenceCheckoutRepository
{
    Task<OpenCheckoutOutcome> OpenAsync(OpenCheckoutParameters parameters, CancellationToken cancellationToken);

    Task<LicenceCheckout?> HeartbeatAsync(
        Guid checkoutId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken
    );

    Task<bool> CloseAsync(
        Guid checkoutId,
        CancellationToken cancellationToken
    );

    Task<bool> ForceRevokeAsync(
        Guid checkoutId,
        LicenceCheckoutCloseReason reason,
        Guid actorUserId,
        string? actorReason,
        CancellationToken cancellationToken
    );

    Task<int> ForceRevokeByLicenceKeyAsync(
        Guid licenceKeyId,
        Guid actorUserId,
        string? actorReason,
        CancellationToken cancellationToken
    );

    Task<ReclaimExpiredResult> ReclaimExpiredAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<LicenceCheckout>> ListLiveForLicenceAsync(
        Guid licenceId,
        CancellationToken cancellationToken
    );

    Task<PagedResult<LicenceCheckoutHistoryEntry>> ListHistoryForLicenceAsync(
        Guid licenceId,
        int limit,
        int offset,
        CancellationToken cancellationToken
    );
}
