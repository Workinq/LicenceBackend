using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Products;

public interface IProductRepository
{
    Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Product?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    Task CreateAsync(Product product, CancellationToken cancellationToken);

    Task<PagedResult<Product>> ListAsync(int limit, int offset, CancellationToken cancellationToken);

    Task UpdateAsync(Product product, CancellationToken cancellationToken);
}
