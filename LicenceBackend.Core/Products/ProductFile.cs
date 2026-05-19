namespace LicenceBackend.Core.Products;

public sealed record ProductFile(
    Guid Id,
    Guid ProductId,
    int VersionNumber,
    string FileName,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    Guid UploadedByAdminId,
    DateTimeOffset UploadedAt
);
