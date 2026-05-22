using System.Text.Json;
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
                           SELECT id, slug, display_name, description, tagline, is_public, price, currency, sort_order, image_path, image_content_type, page_content, created_at
                           FROM products
                           WHERE id = @Id
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(command);
        return row?.ToDomain();
    }

    public async Task<Product?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, slug, display_name, description, tagline, is_public, price, currency, sort_order, image_path, image_content_type, page_content, created_at
                           FROM products
                           WHERE slug = @Slug
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Slug = slug }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(command);
        return row?.ToDomain();
    }

    public async Task CreateAsync(Product product, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO products (id, slug, display_name, description, tagline, is_public, price, currency, sort_order, image_path, image_content_type, page_content, created_at)
                           VALUES (@Id, @Slug, @DisplayName, @Description, @Tagline, @IsPublic, @Price, @Currency, @SortOrder, @ImagePath, @ImageContentType, @PageContent::jsonb, @CreatedAt);
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, ToParameters(product), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<PagedResult<Product>> ListAsync(int limit, int offset, string? q, bool publicOnly, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, slug, display_name, description, tagline, is_public, price, currency, sort_order, image_path, image_content_type, page_content, created_at
                           FROM products
                           WHERE (@Q IS NULL OR display_name ILIKE '%' || @Q || '%')
                             AND (NOT @PublicOnly OR is_public = TRUE)
                           ORDER BY created_at DESC
                           LIMIT @Limit OFFSET @Offset;

                           SELECT COUNT(*) FROM products
                           WHERE (@Q IS NULL OR display_name ILIKE '%' || @Q || '%')
                             AND (NOT @PublicOnly OR is_public = TRUE);
                           """;

        var trimmedQ = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Limit = limit, Offset = offset, Q = trimmedQ, PublicOnly = publicOnly }, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<ProductRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();

        return new PagedResult<Product>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE products
                           SET display_name = @DisplayName,
                               description = @Description,
                               tagline = @Tagline,
                               is_public = @IsPublic,
                               price = @Price,
                               currency = @Currency,
                               sort_order = @SortOrder,
                               image_path = @ImagePath,
                               image_content_type = @ImageContentType,
                               page_content = @PageContent::jsonb
                           WHERE id = @Id;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, ToParameters(product), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private static object ToParameters(Product product) => new
    {
        product.Id,
        product.Slug,
        product.DisplayName,
        product.Description,
        product.Tagline,
        product.IsPublic,
        product.Price,
        product.Currency,
        product.SortOrder,
        product.ImagePath,
        product.ImageContentType,
        product.CreatedAt,
        PageContent = product.PageContent is { } element ? JsonSerializer.Serialize(element) : null,
    };

    private static JsonElement? ParsePageContent(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed record ProductRow(
        Guid Id,
        string Slug,
        string DisplayName,
        string? Description,
        string? Tagline,
        bool IsPublic,
        decimal? Price,
        string Currency,
        int SortOrder,
        string? ImagePath,
        string? ImageContentType,
        string? PageContent,
        DateTime CreatedAt)
    {
        public Product ToDomain()
        {
            return new Product(
                Id,
                Slug,
                DisplayName,
                Description,
                Tagline,
                IsPublic,
                Price,
                Currency.Trim(),
                SortOrder,
                ImagePath,
                ImageContentType,
                TimestampConversion.ToUtcOffset(CreatedAt),
                ParsePageContent(PageContent));
        }
    }
}
