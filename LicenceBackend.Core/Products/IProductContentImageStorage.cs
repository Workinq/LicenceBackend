namespace LicenceBackend.Core.Products;

public interface IProductContentImageStorage
{
    /// <summary>Stores a content image and returns the relative storage path/key.</summary>
    Task<string> SaveAsync(Guid imageId, string fileExtension, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens the stored image for reading, or returns null if it is not there.</summary>
    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken);
}
