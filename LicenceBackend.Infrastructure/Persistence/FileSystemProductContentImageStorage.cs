using LicenceBackend.Core.Products;
using LicenceBackend.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class FileSystemProductContentImageStorage : IProductContentImageStorage
{
    private readonly ProductContentImageStorageOptions _options;

    public FileSystemProductContentImageStorage(IOptions<ProductContentImageStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> SaveAsync(Guid imageId, string fileExtension, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.Directory);
        var storagePath = $"{imageId}{fileExtension}";
        using var dest = File.Create(Path.Combine(_options.Directory, storagePath));
        await content.CopyToAsync(dest, cancellationToken);
        return storagePath;
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken)
    {
        var full = Path.Combine(_options.Directory, storagePath);
        return Task.FromResult<Stream?>(File.Exists(full) ? File.OpenRead(full) : null);
    }
}
