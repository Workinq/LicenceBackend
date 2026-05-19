using System.Data;
using Dapper;
using LicenceBackend.Core.Common;
using LicenceBackend.Core.Orders;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class OrderRepository(NpgsqlDataSource dataSource) : IOrderRepository
{
    private const string Columns = "id, user_id, contact_email, status, created_at";

    public async Task CreateInTxAsync(IDbConnection connection, IDbTransaction transaction, Order order, CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO orders (id, user_id, contact_email, status, created_at)
                           VALUES (@Id, @UserId, @ContactEmail, @Status, @CreatedAt);
                           """;
        var command = new CommandDefinition(
            sql,
            new
            {
                order.Id,
                order.UserId,
                order.ContactEmail,
                Status = order.Status.ToString().ToLowerInvariant(),
                CreatedAt = order.CreatedAt.UtcDateTime
            },
            transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<Order?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM orders
                            WHERE id = @Id
                            LIMIT 1;
                            """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(command);
        return row?.ToDomain();
    }

    public Task<PagedResult<Order>> ListByUserAsync(Guid userId, int limit, int offset, CancellationToken cancellationToken)
    {
        return ListAsync(userId, limit, offset, cancellationToken);
    }

    public async Task<PagedResult<Order>> ListAsync(Guid? userId, int limit, int offset, CancellationToken cancellationToken)
    {
        const string sql = $"""
                            SELECT {Columns}
                            FROM orders
                            WHERE (@UserId::uuid IS NULL OR user_id = @UserId::uuid)
                            ORDER BY created_at DESC, id DESC
                            LIMIT @Limit OFFSET @Offset;

                            SELECT COUNT(*) FROM orders
                            WHERE (@UserId::uuid IS NULL OR user_id = @UserId::uuid);
                            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { UserId = userId, Limit = limit, Offset = offset },
            cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);
        var rows = (await multi.ReadAsync<OrderRow>()).ToList();
        var total = await multi.ReadFirstAsync<int>();
        return new PagedResult<Order>(rows.Select(r => r.ToDomain()).ToList(), total);
    }

    private sealed record OrderRow(Guid Id, Guid UserId, string ContactEmail, string Status, DateTime CreatedAt)
    {
        public Order ToDomain() => new(
            Id,
            UserId,
            ContactEmail,
            Enum.Parse<OrderStatus>(Status, true),
            TimestampConversion.ToUtcOffset(CreatedAt)
        );
    }
}
