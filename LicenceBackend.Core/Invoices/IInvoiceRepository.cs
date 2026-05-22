using System.Data;

namespace LicenceBackend.Core.Invoices;

public interface IInvoiceRepository
{
    Task<long> CreateInTxAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Invoice invoice,
        IReadOnlyList<InvoiceLineItem> lineItems,
        CancellationToken cancellationToken);

    Task<(Invoice Invoice, IReadOnlyList<InvoiceLineItem> LineItems)?> FindByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken);
}
