using System.Data;
using System.Text.Json;
using Dapper;
using LicenceBackend.Core.Payments;
using Npgsql;

namespace LicenceBackend.Infrastructure.Persistence;

public sealed class CheckoutAttemptRepository(NpgsqlDataSource dataSource) : ICheckoutAttemptRepository
{
    private const string AttemptColumns =
        "id, user_id, contact_email, currency, amount_total, stripe_payment_intent_id, status, order_id, created_at, fulfilled_at";

    private const string ItemColumns =
        "id, checkout_attempt_id, product_id, quantity, labels, unit_price, currency";

    public async Task CreateAsync(
        CheckoutAttempt attempt,
        IReadOnlyList<CheckoutAttemptItem> items,
        CancellationToken cancellationToken)
    {
        const string insertAttemptSql = """
                                        INSERT INTO checkout_attempts
                                            (id, user_id, contact_email, currency, amount_total,
                                             stripe_payment_intent_id, status, order_id, created_at, fulfilled_at)
                                        VALUES
                                            (@Id, @UserId, @ContactEmail, @Currency, @AmountTotal,
                                             @StripePaymentIntentId, @Status, @OrderId, @CreatedAt, @FulfilledAt);
                                        """;
        const string insertItemSql = """
                                     INSERT INTO checkout_attempt_items
                                         (id, checkout_attempt_id, product_id, quantity, labels, unit_price, currency)
                                     VALUES
                                         (@Id, @CheckoutAttemptId, @ProductId, @Quantity, @Labels::jsonb, @UnitPrice, @Currency);
                                     """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                insertAttemptSql,
                new
                {
                    attempt.Id,
                    attempt.UserId,
                    attempt.ContactEmail,
                    attempt.Currency,
                    attempt.AmountTotal,
                    attempt.StripePaymentIntentId,
                    Status = attempt.Status.ToString().ToLowerInvariant(),
                    attempt.OrderId,
                    CreatedAt = attempt.CreatedAt.UtcDateTime,
                    FulfilledAt = attempt.FulfilledAt?.UtcDateTime
                },
                transaction,
                cancellationToken: cancellationToken));

            if (items.Count > 0)
            {
                var itemParams = items.Select(i => new
                {
                    i.Id,
                    i.CheckoutAttemptId,
                    i.ProductId,
                    i.Quantity,
                    Labels = JsonSerializer.Serialize(i.Labels),
                    i.UnitPrice,
                    i.Currency
                }).ToArray();
                await connection.ExecuteAsync(new CommandDefinition(
                    insertItemSql, itemParams, transaction, cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CheckoutAttempt?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {AttemptColumns} FROM checkout_attempts WHERE id = @Id LIMIT 1;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AttemptRow>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return row?.ToDomain();
    }

    public async Task<Guid?> FindIdByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id FROM checkout_attempts WHERE stripe_payment_intent_id = @Pi LIMIT 1;";
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { Pi = paymentIntentId }, cancellationToken: cancellationToken));
    }

    public async Task<(CheckoutAttempt Attempt, IReadOnlyList<CheckoutAttemptItem> Items)?> LockByIdInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        var sql = $"""
                   SELECT {AttemptColumns} FROM checkout_attempts WHERE id = @Id FOR UPDATE;
                   SELECT {ItemColumns} FROM checkout_attempt_items WHERE checkout_attempt_id = @Id;
                   """;
        var command = new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken);
        await using var multi = await connection.QueryMultipleAsync(command);

        var attemptRow = await multi.ReadSingleOrDefaultAsync<AttemptRow>();
        if (attemptRow is null) return null;

        var itemRows = (await multi.ReadAsync<ItemRow>()).ToList();
        var items = itemRows.Select(r => r.ToDomain()).ToList();
        return (attemptRow.ToDomain(), items);
    }

    public async Task MarkFulfilledInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid attemptId,
        Guid orderId,
        DateTimeOffset fulfilledAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE checkout_attempts
                           SET status = 'fulfilled', order_id = @OrderId, fulfilled_at = @FulfilledAt
                           WHERE id = @Id;
                           """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = attemptId, OrderId = orderId, FulfilledAt = fulfilledAt.UtcDateTime },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task MarkFailedByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE checkout_attempts
                           SET status = 'failed'
                           WHERE stripe_payment_intent_id = @Pi AND status = 'pending';
                           """;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pi = paymentIntentId }, cancellationToken: cancellationToken));
    }

    private sealed record AttemptRow(
        Guid Id,
        Guid UserId,
        string ContactEmail,
        string Currency,
        decimal AmountTotal,
        string StripePaymentIntentId,
        string Status,
        Guid? OrderId,
        DateTime CreatedAt,
        DateTime? FulfilledAt)
    {
        public CheckoutAttempt ToDomain() => new(
            Id, UserId, ContactEmail, Currency, AmountTotal, StripePaymentIntentId,
            Enum.Parse<CheckoutAttemptStatus>(Status, true), OrderId,
            TimestampConversion.ToUtcOffset(CreatedAt),
            TimestampConversion.ToUtcOffset(FulfilledAt));
    }

    private sealed record ItemRow(
        Guid Id,
        Guid CheckoutAttemptId,
        Guid ProductId,
        int Quantity,
        string Labels,
        decimal? UnitPrice,
        string Currency)
    {
        public CheckoutAttemptItem ToDomain() => new(
            Id, CheckoutAttemptId, ProductId, Quantity,
            JsonSerializer.Deserialize<List<string?>>(Labels) ?? new List<string?>(),
            UnitPrice, Currency);
    }
}
