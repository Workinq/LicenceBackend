import { createFileRoute, Link, useRouterState } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { fetchMyOrder } from '@/api/orders';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import { formatDateTime, formatPrice } from '@/lib/format';
import type { OrderCreatedResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/portal/orders_/$id')({
  component: OrderDetailPage,
});

interface JustPlacedLocationState {
  justPlaced?: OrderCreatedResponse;
}

function OrderDetailPage() {
  const { id } = Route.useParams();
  const locationState = useRouterState({ select: (s) => s.location.state }) as JustPlacedLocationState;
  const justPlaced = locationState?.justPlaced && locationState.justPlaced.id === id ? locationState.justPlaced : undefined;

  const query = useQuery({
    queryKey: ['portal', 'orders', id],
    queryFn: () => fetchMyOrder(id),
  });

  const order = query.data;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Order detail</h1>
          {order && <p className="text-sm text-ink-muted">Placed {formatDateTime(order.createdAt)}</p>}
        </div>
        <Button asChild variant="outline" size="sm">
          <Link to="/portal/orders">Back to orders</Link>
        </Button>
      </div>

      {justPlaced && (
        <Card className="border-accent">
          <CardHeader>
            <CardTitle>Save your licence keys</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-ink">
              These keys are shown once. After you leave this page they cannot be retrieved.
            </p>
            {justPlaced.items.map((item) => (
              <div key={item.id} className="space-y-1">
                <div className="text-xs text-ink-muted">
                  <span className="font-medium text-ink">{item.productDisplayName}</span>
                  {item.label && <span> &middot; {item.label}</span>}
                </div>
                <SecretRevealOnce label="Licence key" value={item.licenceKey} />
              </div>
            ))}
          </CardContent>
        </Card>
      )}

      {query.isPending && <Skeleton className="h-40 w-full" />}
      {query.isError && <p className="text-sm text-status-revoked-fg">Failed to load order.</p>}

      {order && (
        <Card>
          <CardHeader>
            <CardTitle>Summary</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <Field label="Status">
                <span className="capitalize">{order.status}</span>
              </Field>
              <Field label="Contact email">
                <span>{order.contactEmail}</span>
              </Field>
              <Field label="Items">
                <span>{order.items.length}</span>
              </Field>
              <Field label="Total">
                <div className="flex flex-col">
                  {order.totals.map((t) => (
                    <span key={t.currency} className="tabular-nums">{formatPrice(t.amount, t.currency)}</span>
                  ))}
                </div>
              </Field>
            </div>

            <div className="space-y-2">
              <div className="text-xs uppercase tracking-wide text-ink-subtle">Licences</div>
              {order.items.map((item) => (
                <div key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border p-3">
                  <div>
                    <div className="font-medium text-ink">{item.productDisplayName}</div>
                    <div className="font-mono text-[11px] text-ink-muted">{item.productSlug}</div>
                    {item.label && (
                      <div className="mt-1 text-xs text-ink-muted">
                        Label: <span className="text-ink">{item.label}</span>
                      </div>
                    )}
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-sm text-ink-muted">
                      {item.unitPrice != null ? formatPrice(item.unitPrice, item.currency) : 'Free'}
                    </span>
                    <Button asChild variant="link" size="sm" className="px-0">
                      <Link to="/portal/licences/$id" params={{ id: item.licenceId }}>
                        Open licence
                      </Link>
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-xs uppercase tracking-wide text-ink-subtle">{label}</div>
      <div className="text-sm text-ink">{children}</div>
    </div>
  );
}
