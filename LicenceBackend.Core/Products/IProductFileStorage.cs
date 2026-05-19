namespace LicenceBackend.Core.Products;

public interface IProductFileStorage
{
    Task<string> SaveAsync(Guid productFileId, Stream content, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}
