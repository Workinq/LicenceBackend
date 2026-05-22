import { describe, it, expect, vi, beforeEach } from 'vitest';
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

vi.mock('../api/orders', () => ({ fetchMyOrders: vi.fn(), fetchMyOrder: vi.fn(), fetchAdminOrders: vi.fn(), fetchAdminOrder: vi.fn() }));
import { fetchMyOrders } from '../api/orders';
import { Route as PortalOrdersRoute } from '../routes/portal/orders';

type OrderItem = {
  id: string;
  productId: string;
  productSlug: string;
  productDisplayName: string;
  licenceId: string;
  label: string | null;
  unitPrice: number | null;
  currency: string;
};

type Order = {
  id: string;
  userId: string;
  contactEmail: string;
  status: string;
  createdAt: string;
  totals: { amount: number; currency: string }[];
  items: OrderItem[];
};

function order(over: Partial<Order> = {}): Order {
  return {
    id: 'ord-1',
    userId: 'u1',
    contactEmail: 'me@example.com',
    status: 'paid',
    createdAt: '2026-01-01T00:00:00Z',
    totals: [{ amount: 19.99, currency: 'USD' }],
    items: [
      { id: 'i1', productId: 'p1', productSlug: 'acme', productDisplayName: 'Acme', licenceId: 'l1', label: null, unitPrice: 19.99, currency: 'USD' },
    ],
    ...over,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const listRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/orders',
    component: PortalOrdersRoute.options.component,
  });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/orders/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([listRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/orders'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyOrders).mockReset();
});

describe('PortalOrdersPage', () => {
  it('renders the My orders heading and a row per order with item count and status', async () => {
    vi.mocked(fetchMyOrders).mockResolvedValue({
      items: [
        order(),
        order({ id: 'ord-2', status: 'pending', items: [
          { id: 'a', productId: 'p1', productSlug: 'a', productDisplayName: 'A', licenceId: 'l1', label: null, unitPrice: 1, currency: 'USD' },
          { id: 'b', productId: 'p2', productSlug: 'b', productDisplayName: 'B', licenceId: 'l2', label: null, unitPrice: 2, currency: 'USD' },
        ] }),
      ],
      total: 2, limit: 25, offset: 0,
    });
    renderPage();
    expect(await screen.findByRole('heading', { name: /my orders/i })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getAllByRole('link', { name: /^view$/i })).toHaveLength(2);
    });
    expect(screen.getByText('paid')).toBeInTheDocument();
    expect(screen.getByText('pending')).toBeInTheDocument();
  });

  it('shows the empty state when there are no orders', async () => {
    vi.mocked(fetchMyOrders).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderPage();
    expect(await screen.findByText(/haven't placed any orders/i)).toBeInTheDocument();
  });

  it('shows an error row when the query fails', async () => {
    vi.mocked(fetchMyOrders).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText(/failed to load orders/i)).toBeInTheDocument();
  });

  it('disables Previous on the first page and Next when only one page exists', async () => {
    vi.mocked(fetchMyOrders).mockResolvedValue({ items: [order()], total: 1, limit: 25, offset: 0 });
    renderPage();
    await screen.findByRole('heading', { name: /my orders/i });
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
  });

  it('advances the offset when Next is clicked with more pages available', async () => {
    vi.mocked(fetchMyOrders)
      .mockResolvedValueOnce({ items: [order()], total: 100, limit: 25, offset: 0 })
      .mockResolvedValue({ items: [order({ id: 'ord-9' })], total: 100, limit: 25, offset: 25 });
    renderPage();
    await screen.findByRole('heading', { name: /my orders/i });
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    expect(vi.mocked(fetchMyOrders).mock.calls.at(-1)?.[0]).toEqual({ limit: 25, offset: 25 });
  });
});
