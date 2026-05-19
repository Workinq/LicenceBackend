namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record LicenceCheckoutSweeperRanPayload(
    int ReclaimedCount,
    int LicencesAffected
);
