using Dapper;
using LicenceBackend.Core.Products;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class ProductContentImageRepository(NpgsqlDataSource dataSource) : IProductContentImageRepository
{
    private const string Columns =
        "id, product_id, storage_path, content_type, file_size_bytes, uploaded_by_admin_id, uploaded_at";

    public async Task CreateAsync(ProductContentImage image, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            INSERT INTO product_content_images ({Columns})
                            VALUES (@Id, @ProductId, @StoragePath, @ContentType, @FileSizeBytes, @UploadedByAdminId, @UploadedAt);
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                image.Id,
                image.ProductId,
                image.StoragePath,
                image.ContentType,
                image.FileSizeBytes,
                image.UploadedByAdminId,
                UploadedAt = image.UploadedAt.UtcDateTime,
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<ProductContentImage?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM product_content_images
                            WHERE id = @Id
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductContentImageRow>(command);
        return row?.ToDomain();
    }

    private sealed record ProductContentImageRow(
        Guid Id,
        Guid ProductId,
        string StoragePath,
        string ContentType,
        long FileSizeBytes,
        Guid UploadedByAdminId,
        DateTime UploadedAt)
    {
        public ProductContentImage ToDomain() => new(
            Id,
            ProductId,
            StoragePath,
            ContentType,
            FileSizeBytes,
            UploadedByAdminId,
            TimestampConversion.ToUtcOffset(UploadedAt));
    }
}
