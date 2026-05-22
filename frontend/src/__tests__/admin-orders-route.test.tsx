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
import { fetchAdminOrders } from '../api/orders';
import { Route as AdminOrdersRoute } from '../routes/admin/orders';

type Order = {
  id: string;
  userId: string;
  contactEmail: string;
  status: string;
  createdAt: string;
  totals: { amount: number; currency: string }[];
  items: { id: string; productId: string; productSlug: string; productDisplayName: string; licenceId: string; label: string | null; unitPrice: number | null; currency: string }[];
};

function order(over: Partial<Order> = {}): Order {
  return {
    id: 'ord-1',
    userId: 'user-abc',
    contactEmail: 'buyer@example.com',
    status: 'paid',
    createdAt: '2026-01-01T00:00:00Z',
    totals: [{ amount: 19.99, currency: 'USD' }],
    items: [{ id: 'i1', productId: 'p1', productSlug: 'acme', productDisplayName: 'Acme', licenceId: 'l1', label: null, unitPrice: 19.99, currency: 'USD' }],
    ...over,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const listRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/orders',
    component: AdminOrdersRoute.options.component,
  });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/orders/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([listRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/orders'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchAdminOrders).mockReset();
});

describe('AdminOrdersPage', () => {
  it('renders the Orders heading and a row per order with the buyer user id', async () => {
    vi.mocked(fetchAdminOrders).mockResolvedValue({ items: [order()], total: 1, limit: 25, offset: 0 });
    renderPage();
    expect(await screen.findByRole('heading', { name: /^orders$/i })).toBeInTheDocument();
    expect(await screen.findByText('user-abc')).toBeInTheDocument();
    expect(screen.getByText('paid')).toBeInTheDocument();
  });

  it('shows the empty state when no orders match', async () => {
    vi.mocked(fetchAdminOrders).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderPage();
    expect(await screen.findByText(/no orders match/i)).toBeInTheDocument();
  });

  it('shows an error message when the query fails', async () => {
    vi.mocked(fetchAdminOrders).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText(/failed to load orders/i)).toBeInTheDocument();
  });

  it('passes the user filter to fetchAdminOrders and resets the offset', async () => {
    vi.mocked(fetchAdminOrders).mockResolvedValue({ items: [order()], total: 1, limit: 25, offset: 0 });
    renderPage();
    await screen.findByRole('heading', { name: /^orders$/i });
    await userEvent.type(screen.getByPlaceholderText(/filter by user id/i), 'abc');
    await waitFor(() => {
      expect(vi.mocked(fetchAdminOrders).mock.calls.at(-1)?.[0]).toEqual({ limit: 25, offset: 0, userId: 'abc' });
    });
  });

  it('omits userId from the query when the filter input is empty', async () => {
    vi.mocked(fetchAdminOrders).mockResolvedValue({ items: [order()], total: 1, limit: 25, offset: 0 });
    renderPage();
    await screen.findByRole('heading', { name: /^orders$/i });
    expect(vi.mocked(fetchAdminOrders).mock.calls[0][0]).toEqual({ limit: 25, offset: 0, userId: undefined });
  });

  it('disables Next on the last page and Previous on the first', async () => {
    vi.mocked(fetchAdminOrders).mockResolvedValue({ items: [order()], total: 1, limit: 25, offset: 0 });
    renderPage();
    await screen.findByRole('heading', { name: /^orders$/i });
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
  });
});
