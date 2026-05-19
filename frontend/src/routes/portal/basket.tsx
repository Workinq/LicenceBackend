import { createFileRoute, Link } from '@tanstack/react-router';
import { ImageOff, Minus, Plus, ShoppingCart, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  basketCount,
  basketTotalsByCurrency,
  useBasketStore,
} from '@/state/basket-store';
import { formatPrice } from '@/lib/format';

export const Route = createFileRoute('/portal/basket')({
  component: BasketPage,
});

function BasketPage() {
  const items = useBasketStore((s) => s.items);
  const setQuantity = useBasketStore((s) => s.setQuantity);
  const remove = useBasketStore((s) => s.remove);

  const count = basketCount(items);
  const totals = basketTotalsByCurrency(items);

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Your basket</h1>
        <p className="text-sm text-ink-muted">
          {count === 0
            ? 'Your basket is empty.'
            : `${count} unit${count === 1 ? '' : 's'} ready to check out.`}
        </p>
      </div>

      {count === 0 ? (
        <div className="rounded-lg border border-dashed border-border bg-surface-elevated p-8 text-center">
          <ShoppingCart className="mx-auto mb-3 size-8 text-ink-subtle" aria-hidden="true" />
          <p className="text-sm text-ink-muted">Nothing here yet.</p>
          <Button asChild variant="link" className="mt-2">
            <Link to="/portal/products">Browse the catalog</Link>
          </Button>
        </div>
      ) : (
        <>
          <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-16"></TableHead>
                  <TableHead>Product</TableHead>
                  <TableHead className="w-32">Unit price</TableHead>
                  <TableHead className="w-40">Quantity</TableHead>
                  <TableHead className="w-32">Line total</TableHead>
                  <TableHead className="w-12"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((item) => {
                  const unit = item.unitPrice ?? 0;
                  return (
                    <TableRow key={item.productId}>
                      <TableCell>
                        {item.imageUrl ? (
                          <img src={`/api${item.imageUrl}`} alt="" className="size-10 rounded object-cover" />
                        ) : (
                          <div className="flex size-10 items-center justify-center rounded bg-surface-sunken text-ink-subtle">
                            <ImageOff className="size-4" aria-hidden="true" />
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        <div className="font-medium text-ink">{item.displayName}</div>
                        <div className="font-mono text-[11px] text-ink-muted">{item.slug}</div>
                      </TableCell>
                      <TableCell className="text-ink-muted">
                        {item.unitPrice != null ? formatPrice(item.unitPrice, item.currency) : 'Free'}
                      </TableCell>
                      <TableCell>
                        <div className="inline-flex h-8 items-center rounded-md border border-border bg-surface">
                          <button
                            type="button"
                            onClick={() => setQuantity(item.productId, item.quantity - 1)}
                            disabled={item.quantity <= 1}
                            aria-label="Decrease quantity"
                            className="flex h-8 w-8 items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink disabled:cursor-not-allowed disabled:opacity-40"
                          >
                            <Minus className="size-3.5" aria-hidden="true" />
                          </button>
                          <input
                            type="number"
                            min={1}
                            value={item.quantity}
                            onChange={(e) => {
                              const n = Number(e.target.value);
                              if (Number.isFinite(n) && n >= 1) setQuantity(item.productId, Math.floor(n));
                            }}
                            className="h-8 w-14 border-x border-border bg-transparent text-center text-sm tabular-nums focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                          />
                          <button
                            type="button"
                            onClick={() => setQuantity(item.productId, item.quantity + 1)}
                            aria-label="Increase quantity"
                            className="flex h-8 w-8 items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink"
                          >
                            <Plus className="size-3.5" aria-hidden="true" />
                          </button>
                        </div>
                      </TableCell>
                      <TableCell className="text-ink-muted">
                        {item.unitPrice != null ? formatPrice(unit * item.quantity, item.currency) : 'Free'}
                      </TableCell>
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Remove from basket"
                          onClick={() => remove(item.productId)}
                        >
                          <Trash2 className="size-4" aria-hidden="true" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          <div className="flex flex-col items-end gap-3 rounded-lg border border-border bg-surface-elevated p-4">
            <div className="space-y-1 text-right">
              <div className="text-xs uppercase tracking-wide text-ink-subtle">Order total</div>
              {totals.map((t) => (
                <div key={t.currency} className="font-display text-xl font-semibold text-ink">
                  {formatPrice(t.amount, t.currency)}
                </div>
              ))}
            </div>
            <div className="flex gap-2">
              <Button asChild variant="outline">
                <Link to="/portal/products">Keep shopping</Link>
              </Button>
              <Button asChild>
                <Link to="/portal/checkout">Proceed to checkout</Link>
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
