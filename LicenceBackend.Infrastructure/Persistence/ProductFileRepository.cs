using Dapper;
using LicenceBackend.Core.Products;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class ProductFileRepository(NpgsqlDataSource dataSource) : IProductFileRepository
{
    private const string Columns =
        "id, product_id, version_number, file_name, storage_path, content_type, file_size_bytes, uploaded_by_admin_id, uploaded_at";

    public async Task CreateAsync(ProductFile file, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            INSERT INTO product_files ({Columns})
                            VALUES (@Id, @ProductId, @VersionNumber, @FileName, @StoragePath, @ContentType, @FileSizeBytes, @UploadedByAdminId, @UploadedAt);
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                file.Id,
                file.ProductId,
                file.VersionNumber,
                file.FileName,
                file.StoragePath,
                file.ContentType,
                file.FileSizeBytes,
                file.UploadedByAdminId,
                UploadedAt = file.UploadedAt.UtcDateTime
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<ProductFile?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM product_files
                            WHERE id = @Id
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductFileRow>(command);
        return row?.ToDomain();
    }

    public async Task<ProductFile?> GetLatestForProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM product_files
                            WHERE product_id = @ProductId
                            ORDER BY version_number DESC
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductFileRow>(command);
        return row?.ToDomain();
    }

    public async Task<IReadOnlyList<ProductFile>> ListByProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM product_files
                            WHERE product_id = @ProductId
                            ORDER BY version_number DESC;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<ProductFileRow>(command);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<int> GetNextVersionNumberAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT COALESCE(MAX(version_number), 0) + 1
                           FROM product_files
                           WHERE product_id = @ProductId;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    private sealed record ProductFileRow(
        Guid Id,
        Guid ProductId,
        int VersionNumber,
        string FileName,
        string StoragePath,
        string ContentType,
        long FileSizeBytes,
        Guid UploadedByAdminId,
        DateTime UploadedAt)
    {
        public ProductFile ToDomain() => new(
            Id,
            ProductId,
            VersionNumber,
            FileName,
            StoragePath,
            ContentType,
            FileSizeBytes,
            UploadedByAdminId,
            TimestampConversion.ToUtcOffset(UploadedAt));
    }
}
