using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Products;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class ProductRepository(NpgsqlDataSource dataSource) : IProductRepository
{
    public async Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, slug, display_name, created_at
                           FROM products
                           WHERE id = @Id
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var             row        = await connection.QuerySingleOrDefaultAsync<ProductRow>(command);
        return row?.ToDomain();
    }

    public async Task<Product?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, slug, display_name, created_at
                           FROM products
                           WHERE slug = @Slug
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { Slug = slug }, cancellationToken: cancellationToken);
        var             row        = await connection.QuerySingleOrDefaultAsync<ProductRow>(command);
        return row?.ToDomain();
    }

    public async Task CreateAsync(Product product, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO products (id, slug, display_name, created_at)
                           VALUES (@Id, @Slug, @DisplayName, @CreatedAt);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, product, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<Product>> ListAsync(int limit, int offset, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, slug, display_name, created_at
                           FROM products
                           ORDER BY created_at DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM products;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var             command    = new CommandDefinition(sql, new { Limit = limit, Offset = offset }, cancellationToken: cancellationToken);
        await using var multi      = await connection.QueryMultipleAsync(command);
        var             rows       = (await multi.ReadAsync<ProductRow>()).ToList();
        var             total      = await multi.ReadFirstAsync<int>();

        return new PagedResult<Product>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private sealed record ProductRow(Guid Id, string Slug, string DisplayName, DateTime CreatedAt)
    {
        public Product ToDomain()
        {
            return new Product(
                Id,
                Slug,
                DisplayName,
                TimestampConversion.ToUtcOffset(CreatedAt));
        }
    }
}
