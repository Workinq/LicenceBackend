namespace LicenceBackend.Core.Products;

public sealed record ProductContentImage(
    Guid Id,
    Guid ProductId,
    string StoragePath,
    string ContentType,
    long FileSizeBytes,
    Guid UploadedByAdminId,
    DateTimeOffset UploadedAt
);
