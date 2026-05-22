import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

const stripeMock = {
  confirmPayment: vi.fn(),
};
const elementsMock = { tagged: true };

vi.mock('@stripe/stripe-js', () => ({
  loadStripe: vi.fn(() => Promise.resolve({ id: 'stripe-instance' })),
}));

vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <div data-testid="stripe-elements">{children}</div>,
  PaymentElement: () => <div data-testid="payment-element">[payment element]</div>,
  useStripe: () => stripeMock,
  useElements: () => elementsMock,
}));

vi.mock('../api/payments', () => ({
  startCheckout: vi.fn(),
  fetchCheckoutStatus: vi.fn(),
  fetchPaymentConfig: vi.fn(),
}));

import { startCheckout, fetchCheckoutStatus, fetchPaymentConfig } from '../api/payments';
import { Route as CheckoutRoute } from '../routes/portal/checkout';
import { useBasketStore } from '../state/basket-store';
import { useAccessTokenStore } from '../auth/access-token-store';
import type { BasketItem } from '../state/basket-store';

function makeItem(over: Partial<BasketItem> = {}): BasketItem {
  return {
    productId: 'p1',
    slug: 'widget',
    displayName: 'Widget',
    imageUrl: null,
    unitPrice: 1000,
    currency: 'USD',
    quantity: 1,
    labels: [null],
    ...over,
  };
}

function renderCheckout() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const checkoutRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/checkout',
    component: CheckoutRoute.options.component,
  });
  const basketRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/basket',
    component: () => null,
  });
  const orderRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/orders/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([checkoutRoute, basketRoute, orderRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/checkout'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(startCheckout).mockReset();
  vi.mocked(fetchCheckoutStatus).mockReset();
  vi.mocked(fetchPaymentConfig).mockReset();
  stripeMock.confirmPayment.mockReset();
  window.localStorage.clear();
  useBasketStore.setState({ items: [] });
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id: 'u1',
    email: 'u1@example.com',
    displayName: null,
    role: 'user',
    status: 'active',
    createdAt: '2026-01-01T00:00:00Z',
  });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
  window.localStorage.clear();
});

describe('PortalCheckoutRoute', () => {
  it('renders the checkout heading and disables Continue while the config is loading', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockReturnValue(new Promise(() => {}));
    renderCheckout();
    expect(await screen.findByRole('heading', { name: /checkout/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /continue to payment/i })).toBeDisabled();
  });

  it('renders a row per basket item with the display name and slug', async () => {
    useBasketStore.setState({
      items: [
        makeItem({ productId: 'a', displayName: 'Alpha', slug: 'alpha' }),
        makeItem({ productId: 'b', displayName: 'Beta', slug: 'beta', unitPrice: null }),
      ],
    });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    renderCheckout();
    expect(await screen.findByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
    expect(screen.getByText('alpha')).toBeInTheDocument();
    expect(screen.getByText('beta')).toBeInTheDocument();
    expect(screen.getByText(/free/i)).toBeInTheDocument();
  });

  it('shows an error message when startCheckout rejects', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockRejectedValue(new Error('checkout broke'));
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    expect(await screen.findByText(/checkout broke/i)).toBeInTheDocument();
  });

  it('renders the Stripe PaymentElement after startCheckout returns a client secret', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: 'att-1',
      clientSecret: 'pi_secret',
      free: false,
      orderId: null,
    } as never);
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    expect(await screen.findByTestId('payment-element')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^pay$/i })).toBeInTheDocument();
  });

  it('passes the trimmed contact email to startCheckout (or null when blank)', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: 'att-1',
      clientSecret: 'pi_secret',
      free: false,
      orderId: null,
    } as never);
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    await waitFor(() => {
      expect(vi.mocked(startCheckout)).toHaveBeenCalledWith(
        expect.objectContaining({ contactEmail: null }),
      );
    });
  });

  it('forwards a non-blank contact email to startCheckout', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: 'att-1',
      clientSecret: 'pi_secret',
      free: false,
      orderId: null,
    } as never);
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.type(screen.getByLabelText(/contact email/i), 'orders@example.com');
    await userEvent.click(submit);
    await waitFor(() => {
      expect(vi.mocked(startCheckout)).toHaveBeenCalledWith(
        expect.objectContaining({ contactEmail: 'orders@example.com' }),
      );
    });
  });

  it('shows the confirmPayment error when Stripe rejects the payment', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: 'att-1',
      clientSecret: 'pi_secret',
      free: false,
      orderId: null,
    } as never);
    stripeMock.confirmPayment.mockResolvedValue({ error: { message: 'card declined' } });
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    const payBtn = await screen.findByRole('button', { name: /^pay$/i });
    await userEvent.click(payBtn);
    expect(await screen.findByText(/card declined/i)).toBeInTheDocument();
  });

  it('clears the basket and navigates after a successful confirmPayment + fulfilled status', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: 'att-1',
      clientSecret: 'pi_secret',
      free: false,
      orderId: null,
    } as never);
    stripeMock.confirmPayment.mockResolvedValue({});
    vi.mocked(fetchCheckoutStatus).mockResolvedValue({ status: 'fulfilled', orderId: 'ord-9' } as never);
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    const payBtn = await screen.findByRole('button', { name: /^pay$/i });
    await userEvent.click(payBtn);
    await waitFor(() => {
      expect(useBasketStore.getState().items).toHaveLength(0);
    });
  });

  it('takes the free-order shortcut and clears the basket without entering payment', async () => {
    useBasketStore.setState({ items: [makeItem({ unitPrice: null })] });
    vi.mocked(fetchPaymentConfig).mockResolvedValue({ publishableKey: 'pk_test_x' });
    vi.mocked(startCheckout).mockResolvedValue({
      checkoutAttemptId: null,
      clientSecret: null,
      free: true,
      orderId: 'ord-free-1',
    } as never);
    renderCheckout();
    const submit = await screen.findByRole('button', { name: /continue to payment/i });
    await waitFor(() => expect(submit).not.toBeDisabled());
    await userEvent.click(submit);
    await waitFor(() => {
      expect(useBasketStore.getState().items).toHaveLength(0);
    });
    expect(screen.queryByTestId('payment-element')).not.toBeInTheDocument();
  });
});
