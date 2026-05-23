import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/orders', () => ({
  fetchMyOrder: vi.fn(),
  fetchMyOrders: vi.fn(),
  fetchAdminOrder: vi.fn(),
  fetchAdminOrders: vi.fn(),
}));

import { fetchAdminOrder } from '../api/orders';
import { Route as AdminOrderDetailRoute } from '../routes/admin/orders_.$id';
import type { OrderResponse } from '../api/generated/api.schemas';

function order(over: Partial<OrderResponse> = {}): OrderResponse {
  return {
    id: 'ord-1',
    userId: 'user-abc',
    contactEmail: 'buyer@example.com',
    status: 'paid',
    createdAt: '2026-05-01T10:00:00Z',
    totals: [{ currency: 'USD', amount: 19.99 }],
    items: [
      {
        id: 'i-1',
        productId: 'p-1',
        productSlug: 'acme-pro',
        productDisplayName: 'Acme Pro',
        licenceId: 'lic-1',
        label: null,
        unitPrice: 19.99,
        currency: 'USD',
      },
    ],
    ...over,
  };
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = AdminOrderDetailRoute.update({
    id: '/admin/orders_/$id',
    path: '/admin/orders/$id',
    getParentRoute: () => rootRoute,
  } as never);
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/orders', component: () => null });
  const invoiceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/orders/$id/invoice', component: () => null });
  const licenceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/licences/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute as never, listRoute, invoiceRoute, licenceRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/orders/ord-1'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchAdminOrder).mockReset();
});

describe('AdminOrderDetailRoute', () => {
  it('renders a loading skeleton while the order query is pending', async () => {
    vi.mocked(fetchAdminOrder).mockReturnValue(new Promise(() => {}));
    const { container } = renderDetail();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the query errors', async () => {
    vi.mocked(fetchAdminOrder).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load order/i)).toBeInTheDocument();
  });

  it('renders the order summary including the buyer user id and contact email', async () => {
    vi.mocked(fetchAdminOrder).mockResolvedValue(order());
    renderDetail();
    expect(await screen.findByRole('heading', { name: /order detail/i })).toBeInTheDocument();
    expect(await screen.findByText('user-abc')).toBeInTheDocument();
    expect(screen.getByText('buyer@example.com')).toBeInTheDocument();
    expect(screen.getByText('paid')).toBeInTheDocument();
  });

  it('renders a View invoice link to the admin invoice route', async () => {
    vi.mocked(fetchAdminOrder).mockResolvedValue(order());
    renderDetail();
    const link = await screen.findByRole('link', { name: /view invoice/i });
    expect(link.getAttribute('href')).toBe('/admin/orders/ord-1/invoice');
  });

  it('renders an Open licence link per item to the admin licence route', async () => {
    vi.mocked(fetchAdminOrder).mockResolvedValue(order());
    renderDetail();
    const link = await screen.findByRole('link', { name: /open licence/i });
    expect(link.getAttribute('href')).toBe('/admin/licences/lic-1');
  });

  it('renders the label and Free price when the item has a label and no unit price', async () => {
    vi.mocked(fetchAdminOrder).mockResolvedValue(
      order({
        items: [
          {
            id: 'i-1',
            productId: 'p-1',
            productSlug: 'acme-pro',
            productDisplayName: 'Acme Pro',
            licenceId: 'lic-1',
            label: 'Team A',
            unitPrice: null,
            currency: 'USD',
          },
        ],
      }),
    );
    renderDetail();
    expect(await screen.findByText('Team A')).toBeInTheDocument();
    expect(screen.getByText(/^free$/i)).toBeInTheDocument();
  });
});
