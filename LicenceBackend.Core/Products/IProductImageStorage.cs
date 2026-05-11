namespace LicenceBackend.Core.Products;

public interface IProductImageStorage
{
    /// <summary>Stores the image for <paramref name="productId" /> and returns the relative storage path/key.</summary>
    Task<string> SaveAsync(Guid productId, string fileExtension, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens the stored image for reading, or returns null if it is not there.</summary>
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken);
}
