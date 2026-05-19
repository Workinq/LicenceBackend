namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record ProductFileDownloadedPayload(
    Guid ProductFileId,
    int VersionNumber,
    Guid LicenceId
);
