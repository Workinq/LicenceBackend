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

import { fetchMyOrder } from '../api/orders';
import { Route as OrderDetailRoute } from '../routes/portal/orders_.$id';
import type { OrderResponse } from '../api/generated/api.schemas';

function order(over: Partial<OrderResponse> = {}): OrderResponse {
  return {
    id: 'ord-1',
    userId: 'u-1',
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
  const detailRoute = OrderDetailRoute.update({
    id: '/portal/orders_/$id',
    path: '/portal/orders/$id',
    getParentRoute: () => rootRoute,
  } as never);
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/orders', component: () => null });
  const invoiceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/orders/$id/invoice', component: () => null });
  const licenceRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/licences/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute as never, listRoute, invoiceRoute, licenceRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/orders/ord-1'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyOrder).mockReset();
});

describe('PortalOrderDetailRoute', () => {
  it('renders a loading skeleton while the order is pending', async () => {
    vi.mocked(fetchMyOrder).mockReturnValue(new Promise(() => {}));
    const { container } = renderDetail();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the order query errors', async () => {
    vi.mocked(fetchMyOrder).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load order/i)).toBeInTheDocument();
  });

  it('renders the order summary fields once loaded', async () => {
    vi.mocked(fetchMyOrder).mockResolvedValue(order());
    renderDetail();
    expect(await screen.findByRole('heading', { name: /order detail/i })).toBeInTheDocument();
    expect(await screen.findByText('paid')).toBeInTheDocument();
    expect(screen.getByText('buyer@example.com')).toBeInTheDocument();
    expect(screen.getByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('acme-pro')).toBeInTheDocument();
  });

  it('renders a View invoice link to the invoice route', async () => {
    vi.mocked(fetchMyOrder).mockResolvedValue(order());
    renderDetail();
    const link = await screen.findByRole('link', { name: /view invoice/i });
    expect(link.getAttribute('href')).toBe('/portal/orders/ord-1/invoice');
  });

  it('renders an Open licence link per item with the correct href', async () => {
    vi.mocked(fetchMyOrder).mockResolvedValue(order());
    renderDetail();
    const link = await screen.findByRole('link', { name: /open licence/i });
    expect(link.getAttribute('href')).toBe('/portal/licences/lic-1');
  });

  it('renders Free when the item unit price is null', async () => {
    vi.mocked(fetchMyOrder).mockResolvedValue(
      order({
        items: [
          {
            id: 'i-1',
            productId: 'p-1',
            productSlug: 'acme-pro',
            productDisplayName: 'Acme Pro',
            licenceId: 'lic-1',
            label: 'Personal',
            unitPrice: null,
            currency: 'USD',
          },
        ],
      }),
    );
    renderDetail();
    expect(await screen.findByText(/^free$/i)).toBeInTheDocument();
    expect(screen.getByText(/personal/i)).toBeInTheDocument();
  });
});
