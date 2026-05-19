namespace LicenceBackend.Core.Products;

public interface IProductFileRepository
{
    Task CreateAsync(ProductFile file, CancellationToken cancellationToken);

    Task<ProductFile?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductFile?> GetLatestForProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductFile>> ListByProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<int> GetNextVersionNumberAsync(Guid productId, CancellationToken cancellationToken);
}
