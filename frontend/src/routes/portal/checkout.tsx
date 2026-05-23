import { useMemo, useRef, useState } from 'react';
import { createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { loadStripe } from '@stripe/stripe-js';
import { Elements, PaymentElement, useElements, useStripe } from '@stripe/react-stripe-js';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { basketTotalsByCurrency, useBasketStore } from '@/state/basket-store';
import { formatPrice } from '@/lib/format';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { startCheckout, fetchCheckoutStatus, fetchPaymentConfig } from '@/api/payments';
import type { CreateOrderRequest } from '@/api/generated/api.schemas';

function readBasketFromStorage(): unknown[] {
  if (typeof globalThis.window === 'undefined') return [];
  const uid = useAccessTokenStore.getState().user?.id;
  if (!uid) return [];
  const raw = globalThis.localStorage.getItem(`basket:${uid}`);
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

async function pollUntilFulfilled(attemptId: string): Promise<string> {
  for (let i = 0; i < 40; i++) {
    const status = await fetchCheckoutStatus(attemptId);
    if (status.status === 'fulfilled' && status.orderId) return status.orderId;
    if (status.status === 'failed') throw new Error('Payment could not be completed.');
    await new Promise((r) => setTimeout(r, 1500));
  }
  throw new Error('Timed out waiting for your order to be confirmed.');
}

function CheckoutPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAccessTokenStore((s) => s.user);
  const items = useBasketStore((s) => s.items);
  const setLabel = useBasketStore((s) => s.setLabel);
  const clear = useBasketStore((s) => s.clear);

  const [contactEmail, setContactEmail] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const startingRef = useRef(false);

  const totals = basketTotalsByCurrency(items);
  const accountEmail = user?.email ?? '';

  const configQuery = useQuery({
    queryKey: ['payments', 'config'],
    queryFn: fetchPaymentConfig,
  });

  const stripePromise = useMemo(
    () => (configQuery.data ? loadStripe(configQuery.data.publishableKey) : null),
    [configQuery.data],
  );

  const finishFree = async (orderId: string) => {
    clear();
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] }),
      queryClient.invalidateQueries({ queryKey: ['portal', 'licences'] }),
      queryClient.invalidateQueries({ queryKey: ['portal', 'overview'] }),
    ]);
    await navigate({ to: '/portal/orders/$id', params: { id: orderId } });
  };

  const onContinue = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (startingRef.current) return;
    startingRef.current = true;
    setError(null);
    setStarting(true);
    void runCheckout();
  };

  const runCheckout = async () => {
    try {
      const body: CreateOrderRequest = {
        contactEmail: contactEmail.trim() === '' ? null : contactEmail.trim(),
        items: items.map((i) => ({
          productId: i.productId,
          quantity: i.quantity,
          labels: i.labels.map((l) => l ?? ''),
        })),
      };
      const session = await startCheckout(body);
      if (session.free && session.orderId) {
        await finishFree(session.orderId);
        return;
      }
      setClientSecret(session.clientSecret ?? null);
      setAttemptId(session.checkoutAttemptId ?? null);
    } catch (err: unknown) {
      setError(err instanceof Error && err.message ? err.message : 'Could not start checkout.');
    } finally {
      setStarting(false);
      startingRef.current = false;
    }
  };

  const inPayment = clientSecret !== null && attemptId !== null;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Checkout</h1>
        <p className="text-sm text-ink-muted">Review your order, name each licence, and pay.</p>
      </div>

      {!inPayment && (
        <form onSubmit={onContinue} className="space-y-4">
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
                    {item.unitPrice == null ? 'Free' : formatPrice(item.unitPrice, item.currency)}
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
            <Button type="submit" disabled={starting || configQuery.isPending}>
              {starting ? 'Starting...' : 'Continue to payment'}
            </Button>
          </section>
        </form>
      )}

      {inPayment && stripePromise && clientSecret && attemptId && (
        <Elements stripe={stripePromise} options={{ clientSecret }}>
          <PaymentStep
            attemptId={attemptId}
            onPaid={async (orderId) => {
              clear();
              await Promise.all([
                queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] }),
                queryClient.invalidateQueries({ queryKey: ['portal', 'licences'] }),
                queryClient.invalidateQueries({ queryKey: ['portal', 'overview'] }),
              ]);
              await navigate({ to: '/portal/orders/$id', params: { id: orderId } });
            }}
          />
        </Elements>
      )}
    </div>
  );
}

function PaymentStep({ attemptId, onPaid }: Readonly<{ attemptId: string; onPaid: (orderId: string) => Promise<void> }>) {
  const stripe = useStripe();
  const elements = useElements();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onPay = () => {
    void runPayment();
  };

  const runPayment = async () => {
    if (!stripe || !elements) return;
    setBusy(true);
    setError(null);
    const result = await stripe.confirmPayment({ elements, redirect: 'if_required' });
    if (result.error) {
      setError(result.error.message ?? 'Your payment could not be completed.');
      setBusy(false);
      return;
    }
    try {
      const orderId = await pollUntilFulfilled(attemptId);
      await onPaid(orderId);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Something went wrong confirming your order.');
      setBusy(false);
    }
  };

  return (
    <section className="space-y-4 rounded-lg border border-border bg-surface-elevated p-4">
      <PaymentElement />
      {error && <p className="text-sm text-status-revoked-fg">{error}</p>}
      <div className="flex justify-end">
        <Button type="button" onClick={onPay} disabled={busy || !stripe || !elements}>
          {busy ? 'Processing...' : 'Pay'}
        </Button>
      </div>
    </section>
  );
}
