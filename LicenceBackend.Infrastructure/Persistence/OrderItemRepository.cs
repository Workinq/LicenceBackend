using System.Data;
using Dapper;
using LicenceBackend.Core.Orders;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class OrderItemRepository(NpgsqlDataSource dataSource) : IOrderItemRepository
{
    private const string Columns = "id, order_id, product_id, licence_id, unit_price, currency, created_at";

    public async Task BulkCreateInTxAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        const string sql = """
                           INSERT INTO order_items (id, order_id, product_id, licence_id, unit_price, currency, created_at)
                           VALUES (@Id, @OrderId, @ProductId, @LicenceId, @UnitPrice, @Currency, @CreatedAt);
                           """;

        var parameters = items.Select(i => new
        {
            i.Id,
            i.OrderId,
            i.ProductId,
            i.LicenceId,
            i.UnitPrice,
            i.Currency,
            CreatedAt = i.CreatedAt.UtcDateTime
        }).ToArray();

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<IReadOnlyList<OrderItem>> ListByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0) return Array.Empty<OrderItem>();

        const string sql = $"""
                            SELECT {Columns}
                            FROM order_items
                            WHERE order_id = ANY(@OrderIds)
                            ORDER BY order_id, created_at, id;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { OrderIds = orderIds.ToArray() },
            cancellationToken: cancellationToken);
        var rows = (await connection.QueryAsync<OrderItemRow>(command)).ToList();
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<Guid?> FindOrderIdByLicenceIdAsync(Guid licenceId, CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT order_id
                           FROM order_items
                           WHERE licence_id = @LicenceId
                           LIMIT 1;
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { LicenceId = licenceId }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(command);
    }

    private sealed record OrderItemRow(
        Guid Id,
        Guid OrderId,
        Guid ProductId,
        Guid LicenceId,
        decimal? UnitPrice,
        string Currency,
        DateTime CreatedAt
    )
    {
        public OrderItem ToDomain() => new(
            Id,
            OrderId,
            ProductId,
            LicenceId,
            UnitPrice,
            Currency,
            TimestampConversion.ToUtcOffset(CreatedAt)
        );
    }
}
