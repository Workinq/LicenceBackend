namespace LicenceBackend.Core.Auditing.Payloads;

public sealed record ProductFileUploadedPayload(
    Guid ProductFileId,
    int VersionNumber,
    string FileName,
    string ContentType,
    long FileSizeBytes
);
