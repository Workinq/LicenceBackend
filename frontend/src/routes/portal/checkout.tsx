import { useState } from 'react';
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  basketTotalsByCurrency,
  useBasketStore,
} from '@/state/basket-store';
import { formatPrice } from '@/lib/format';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { placeOrder } from '@/api/orders';
import type { CreateOrderRequest, OrderCreatedResponse } from '@/api/generated/api.schemas';

function readBasketFromStorage(): unknown[] {
  if (typeof window === 'undefined') return [];
  const uid = useAccessTokenStore.getState().user?.id;
  if (!uid) return [];
  const raw = window.localStorage.getItem(`basket:${uid}`);
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export const Route = createFileRoute('/portal/checkout')({
  beforeLoad: () => {
    if (readBasketFromStorage().length === 0) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/portal/basket' });
    }
  },
  component: CheckoutPage,
});

function CheckoutPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAccessTokenStore((s) => s.user);
  const items = useBasketStore((s) => s.items);
  const setLabel = useBasketStore((s) => s.setLabel);
  const clear = useBasketStore((s) => s.clear);

  const [contactEmail, setContactEmail] = useState('');
  const [error, setError] = useState<string | null>(null);

  const totals = basketTotalsByCurrency(items);
  const accountEmail = user?.email ?? '';

  const mutation = useMutation({
    mutationFn: async (body: CreateOrderRequest) => placeOrder(body),
    onSuccess: (order: OrderCreatedResponse) => {
      clear();
      void queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] });
      void queryClient.invalidateQueries({ queryKey: ['portal', 'licences'] });
      void queryClient.invalidateQueries({ queryKey: ['portal', 'overview'] });
      void navigate({
        to: '/portal/orders/$id',
        params: { id: order.id },
        state: { justPlaced: order } as never,
      });
    },
    onError: (err: unknown) => {
      const message =
        err instanceof Error && err.message
          ? err.message
          : 'Order failed. Your basket has been preserved — please try again.';
      setError(message);
    },
  });

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (items.length === 0) return;
    const body: CreateOrderRequest = {
      contactEmail: contactEmail.trim() === '' ? null : contactEmail.trim(),
      items: items.map((i) => ({
        productId: i.productId,
        quantity: i.quantity,
        labels: i.labels.map((l) => l ?? ''),
      })),
    };
    mutation.mutate(body);
  };

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Checkout</h1>
        <p className="text-sm text-ink-muted">Review your order, name each licence, and confirm.</p>
      </div>

      <form onSubmit={onSubmit} className="space-y-4">
        <section className="space-y-2 rounded-lg border border-border bg-surface-elevated p-4">
          <Label htmlFor="contactEmail">Contact email (optional)</Label>
          <Input
            id="contactEmail"
            type="email"
            placeholder={accountEmail}
            value={contactEmail}
            onChange={(e) => setContactEmail(e.target.value)}
            autoComplete="email"
          />
          <p className="text-xs text-ink-muted">
            Used for the order receipt. Defaults to your account email ({accountEmail}) when left blank.
          </p>
        </section>

        <section className="space-y-3">
          {items.map((item) => (
            <div key={item.productId} className="rounded-lg border border-border bg-surface-elevated p-4">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <div>
                  <div className="font-medium text-ink">{item.displayName}</div>
                  <div className="font-mono text-[11px] text-ink-muted">{item.slug}</div>
                </div>
                <div className="text-sm text-ink-muted">
                  {item.quantity} &times;{' '}
                  {item.unitPrice != null ? formatPrice(item.unitPrice, item.currency) : 'Free'}
                </div>
              </div>
              <div className="mt-3 space-y-2">
                <div className="text-xs uppercase tracking-wide text-ink-subtle">
                  Labels (optional, one per licence)
                </div>
                <div className="grid gap-2 sm:grid-cols-2">
                  {Array.from({ length: item.quantity }, (_, idx) => (
                    <Input
                      key={idx}
                      placeholder={`Licence ${idx + 1} name`}
                      value={item.labels[idx] ?? ''}
                      onChange={(e) => {
                        const v = e.target.value;
                        setLabel(item.productId, idx, v === '' ? null : v);
                      }}
                      maxLength={10}
                    />
                  ))}
                </div>
              </div>
            </div>
          ))}
        </section>

        <section className="flex flex-col items-end gap-3 rounded-lg border border-border bg-surface-elevated p-4">
          <div className="space-y-1 text-right">
            <div className="text-xs uppercase tracking-wide text-ink-subtle">Order total</div>
            {totals.map((t) => (
              <div key={t.currency} className="font-display text-xl font-semibold text-ink">
                {formatPrice(t.amount, t.currency)}
              </div>
            ))}
          </div>
          {error && <p className="text-sm text-status-revoked-fg">{error}</p>}
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? 'Placing order...' : 'Complete order'}
          </Button>
        </section>
      </form>
    </div>
  );
}
