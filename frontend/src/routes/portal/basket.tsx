import { createFileRoute, Link } from '@tanstack/react-router';
import { ArrowRight, ImageOff, Minus, Plus, ShoppingCart, Trash2 } from 'lucide-react';
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
    <div className="mx-auto max-w-6xl space-y-4">
      <div>
        <h1 className="text-[22px] font-semibold tracking-tight text-foreground">Your basket</h1>
        <p className="text-[12.5px] text-ink-muted">
          {count === 0
            ? 'Your basket is empty.'
            : `${count} unit${count === 1 ? '' : 's'} ready to check out.`}
        </p>
      </div>

      {count === 0 ? (
        <div className="rounded-md border border-dashed border-border bg-card p-8 text-center">
          <ShoppingCart className="mx-auto mb-3 size-8 text-ink-subtle" aria-hidden="true" />
          <p className="text-[12.5px] text-ink-muted">Nothing here yet.</p>
          <Button asChild variant="link" className="mt-2">
            <Link to="/portal/products">Browse the catalog</Link>
          </Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_320px]">
          <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
            <Table className="text-[12.5px]">
              <TableHeader>
                <TableRow className="border-border">
                  <TableHead className="h-9 w-16 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted"></TableHead>
                  <TableHead className="h-9 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Product</TableHead>
                  <TableHead className="h-9 w-32 px-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Unit</TableHead>
                  <TableHead className="h-9 w-40 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Quantity</TableHead>
                  <TableHead className="h-9 w-32 px-3 text-right text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted">Total</TableHead>
                  <TableHead className="h-9 w-12 px-3"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((item) => {
                  const unit = item.unitPrice ?? 0;
                  return (
                    <TableRow key={item.productId} className="border-border">
                      <TableCell className="px-3 py-2.5">
                        {item.imageUrl ? (
                          <img src={`/api${item.imageUrl}`} alt="" className="size-9 rounded object-cover" />
                        ) : (
                          <div className="flex size-9 items-center justify-center rounded bg-surface-sunken text-ink-subtle">
                            <ImageOff className="size-4" aria-hidden="true" />
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="px-3 py-2.5">
                        <div className="font-medium text-foreground">{item.displayName}</div>
                        <div className="font-mono text-[11px] text-ink-muted">{item.slug}</div>
                      </TableCell>
                      <TableCell className="px-3 py-2.5 text-right font-mono tabular-nums text-ink-muted">
                        {item.unitPrice != null ? formatPrice(item.unitPrice, item.currency) : 'Free'}
                      </TableCell>
                      <TableCell className="px-3 py-2.5">
                        <div className="inline-flex h-7 items-center rounded-[4px] border border-border bg-card">
                          <button
                            type="button"
                            onClick={() => setQuantity(item.productId, item.quantity - 1)}
                            disabled={item.quantity <= 1}
                            aria-label="Decrease quantity"
                            className="flex h-7 w-7 items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-foreground disabled:cursor-not-allowed disabled:opacity-40"
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
                            className="h-7 w-12 border-x border-border bg-transparent text-center font-mono text-[12px] tabular-nums focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                          />
                          <button
                            type="button"
                            onClick={() => setQuantity(item.productId, item.quantity + 1)}
                            aria-label="Increase quantity"
                            className="flex h-7 w-7 items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-foreground"
                          >
                            <Plus className="size-3.5" aria-hidden="true" />
                          </button>
                        </div>
                      </TableCell>
                      <TableCell className="px-3 py-2.5 text-right font-mono tabular-nums text-foreground">
                        {item.unitPrice != null ? formatPrice(unit * item.quantity, item.currency) : 'Free'}
                      </TableCell>
                      <TableCell className="px-3 py-2.5">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="size-7"
                          aria-label="Remove from basket"
                          onClick={() => remove(item.productId)}
                        >
                          <Trash2 className="size-3.5" aria-hidden="true" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          <aside className="self-start rounded-md border border-border bg-card p-4 shadow-card">
            <h2 className="text-[13px] font-semibold text-foreground">Summary</h2>
            <dl className="mt-3 space-y-2 text-[12.5px]">
              {totals.map((t) => (
                <div key={`sub-${t.currency}`} className="flex items-center justify-between">
                  <dt className="text-ink-muted">Subtotal</dt>
                  <dd className="font-mono tabular-nums">{formatPrice(t.amount, t.currency)}</dd>
                </div>
              ))}
              <div className="flex items-center justify-between">
                <dt className="text-ink-muted">Tax</dt>
                <dd className="font-mono tabular-nums text-ink-muted">-</dd>
              </div>
              <div className="flex items-center justify-between">
                <dt className="text-ink-muted">Discount</dt>
                <dd className="font-mono tabular-nums text-ink-muted">-</dd>
              </div>
              <div className="border-t border-border pt-2.5">
                {totals.map((t) => (
                  <div key={`tot-${t.currency}`} className="flex items-center justify-between">
                    <dt className="font-semibold text-foreground">Total</dt>
                    <dd className="font-mono text-[14px] font-semibold tabular-nums text-foreground">
                      {formatPrice(t.amount, t.currency)}
                    </dd>
                  </div>
                ))}
              </div>
            </dl>
            <Button asChild className="mt-4 w-full justify-center gap-1.5">
              <Link to="/portal/checkout">
                Proceed to checkout <ArrowRight className="size-3.5" />
              </Link>
            </Button>
            <Button asChild variant="ghost" className="mt-1.5 w-full justify-center text-[12px]">
              <Link to="/portal/products">Keep shopping</Link>
            </Button>
          </aside>
        </div>
      )}
    </div>
  );
}
