namespace LicenceBackend.Core.Products;

public interface IProductContentImageRepository
{
    Task CreateAsync(ProductContentImage image, CancellationToken cancellationToken);

    Task<ProductContentImage?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
