namespace LicenceBackend.Api.Models.Response;

public sealed record ProductFileResponse(
    Guid Id,
    Guid ProductId,
    int VersionNumber,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Guid UploadedByAdminId,
    DateTimeOffset UploadedAt
);
