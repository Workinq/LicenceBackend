import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { InvoiceDocument } from '@/components/InvoiceDocument';
import { fetchAdminInvoice } from '@/api/invoices';

export const Route = createFileRoute('/admin/orders_/$id/invoice')({
  component: AdminInvoicePage,
});

function AdminInvoicePage() {
  const { id } = Route.useParams();
  const query = useQuery({
    queryKey: ['admin', 'orders', id, 'invoice'],
    queryFn: () => fetchAdminInvoice(id),
  });

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="print:hidden">
        <Button asChild variant="outline" size="sm">
          <Link to="/admin/orders/$id" params={{ id }}>
            Back to order
          </Link>
        </Button>
      </div>
      {query.isPending && <Skeleton className="h-96 w-full" />}
      {query.isError && <p className="text-sm text-status-revoked-fg">Failed to load invoice.</p>}
      {query.data && <InvoiceDocument invoice={query.data} />}
    </div>
  );
}
