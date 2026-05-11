using LicenceBackend.Core.Products;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class FileSystemProductImageStorage : IProductImageStorage
{
    private readonly ProductImageStorageOptions _options;

    public FileSystemProductImageStorage(IOptions<ProductImageStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(Guid productId, string fileExtension, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.Directory);
        var storagePath = $"{productId}{fileExtension}";
        using var dest = File.Create(Path.Combine(_options.Directory, storagePath));
        await content.CopyToAsync(dest, cancellationToken);
        return storagePath;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var full = Path.Combine(_options.Directory, storagePath);
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        var full = Path.Combine(_options.Directory, storagePath);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}
