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

vi.mock('../api/invoices', () => ({
  fetchMyInvoice: vi.fn(),
  fetchAdminInvoice: vi.fn(),
}));

import { fetchMyInvoice } from '../api/invoices';
import { Route as PortalInvoiceRoute } from '../routes/portal/orders_.$id_.invoice';
import type { InvoiceResponse } from '../api/generated/api.schemas';

function invoice(over: Partial<InvoiceResponse> = {}): InvoiceResponse {
  return {
    orderId: 'ord-1',
    invoiceNumber: 'INV-2026-0001',
    issuedAt: '2026-05-01T10:00:00Z',
    status: 'paid',
    seller: {
      name: 'Acme Software',
      addressLine1: '1 Way',
      addressLine2: '',
      city: 'Town',
      region: 'RG',
      postalCode: '12345',
      country: 'UK',
    },
    buyer: {
      contactEmail: 'buyer@example.com',
      name: null,
      addressLine1: null,
      addressLine2: null,
      city: null,
      region: null,
      postalCode: null,
      country: null,
    },
    lineItems: [
      { licenceId: 'lic-1', productId: 'p-1', productName: 'Acme Pro', productSlug: 'acme-pro', label: null, unitPrice: 19.99, currency: 'USD' },
    ],
    totals: [{ currency: 'USD', amount: 19.99 }],
    ...over,
  };
}

function renderInvoice() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const invoiceRoute = PortalInvoiceRoute.update({
    id: '/portal/orders_/$id/invoice',
    path: '/portal/orders/$id/invoice',
    getParentRoute: () => rootRoute,
  } as never);
  const orderRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/orders/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([invoiceRoute as never, orderRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/orders/ord-1/invoice'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyInvoice).mockReset();
});

describe('PortalInvoiceRoute', () => {
  it('renders a loading skeleton while the invoice is pending', async () => {
    vi.mocked(fetchMyInvoice).mockReturnValue(new Promise(() => {}));
    const { container } = renderInvoice();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the invoice query errors', async () => {
    vi.mocked(fetchMyInvoice).mockRejectedValue(new Error('boom'));
    renderInvoice();
    expect(await screen.findByText(/failed to load invoice/i)).toBeInTheDocument();
  });

  it('renders the invoice document with the invoice number once loaded', async () => {
    vi.mocked(fetchMyInvoice).mockResolvedValue(invoice());
    renderInvoice();
    expect(await screen.findByText('INV-2026-0001')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /^invoice$/i })).toBeInTheDocument();
  });

  it('renders a back-to-order link to the portal order detail route', async () => {
    vi.mocked(fetchMyInvoice).mockResolvedValue(invoice());
    renderInvoice();
    const link = await screen.findByRole('link', { name: /back to order/i });
    expect(link.getAttribute('href')).toBe('/portal/orders/ord-1');
  });

  it('passes the order id from the URL through to fetchMyInvoice', async () => {
    vi.mocked(fetchMyInvoice).mockResolvedValue(invoice());
    renderInvoice();
    await waitFor(() => {
      expect(vi.mocked(fetchMyInvoice)).toHaveBeenCalledWith('ord-1');
    });
  });
});
