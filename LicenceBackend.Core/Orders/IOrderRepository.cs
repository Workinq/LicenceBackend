using System.Data;
using LicenceBackend.Core.Common;

namespace LicenceBackend.Core.Orders;

public interface IOrderRepository
{
    Task CreateInTxAsync(IDbConnection connection, IDbTransaction transaction, Order order, CancellationToken cancellationToken);

    Task<Order?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<Order>> ListByUserAsync(Guid userId, int limit, int offset, CancellationToken cancellationToken);

    Task<PagedResult<Order>> ListAsync(Guid? userId, int limit, int offset, CancellationToken cancellationToken);
}
