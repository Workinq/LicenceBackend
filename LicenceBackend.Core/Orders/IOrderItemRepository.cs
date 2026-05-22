using System.Data;

namespace LicenceBackend.Core.Orders;

public interface IOrderItemRepository
{
    Task BulkCreateInTxAsync(IDbConnection connection, IDbTransaction transaction, IReadOnlyList<OrderItem> items, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderItem>> ListByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

    Task<Guid?> FindOrderIdByLicenceIdAsync(Guid licenceId, CancellationToken cancellationToken);
}
