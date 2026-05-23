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

vi.mock('../api/licences', () => ({
  fetchLicence: vi.fn(),
  fetchLicenceMembers: vi.fn(),
  fetchLicenceSeats: vi.fn(),
  fetchLicenceStatusHistory: vi.fn(),
  fetchLicenceBindingHistory: vi.fn(),
  fetchLicenceVerificationAttempts: vi.fn(),
  addLicenceMember: vi.fn(),
  removeLicenceMember: vi.fn(),
  forceRevokeSeat: vi.fn(),
  updateLicenceMaxSeats: vi.fn(),
  updateLicenceStatus: vi.fn(),
  updateLicenceHwid: vi.fn(),
  updateLicenceIpAllowlist: vi.fn(),
  regenerateLicenceKey: vi.fn(),
}));
vi.mock('../api/audit-events', () => ({ fetchAuditEvents: vi.fn() }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));

import {
  fetchLicence,
  fetchLicenceMembers,
  fetchLicenceSeats,
  fetchLicenceStatusHistory,
  fetchLicenceBindingHistory,
  fetchLicenceVerificationAttempts,
} from '../api/licences';
import { fetchAuditEvents } from '../api/audit-events';
import { Route as AdminLicenceDetailRoute } from '../routes/admin/licences_.$id';
import type { LicenceResponse } from '../api/generated/api.schemas';

function licence(over: Partial<LicenceResponse> = {}): LicenceResponse {
  return {
    id: 'lic-1',
    productId: 'p-1',
    productSlug: 'acme-pro',
    userId: 'u-1',
    userEmail: 'owner@example.com',
    status: 'active',
    expiresAt: null,
    notes: null,
    hwidBound: false,
    hasKey: true,
    ipAllowlist: null,
    label: null,
    createdAt: '2026-01-01T00:00:00Z',
    orderId: null,
    relationship: 'owner',
    ...over,
  };
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = AdminLicenceDetailRoute.update({
    id: '/admin/licences_/$id',
    path: '/admin/licences/$id',
    getParentRoute: () => rootRoute,
  } as never);
  const productRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products/$id', component: () => null });
  const orderRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/orders/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute as never, productRoute, orderRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/licences/lic-1'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchLicence).mockReset();
  vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
  vi.mocked(fetchLicenceSeats).mockResolvedValue({
    maxSeats: 5,
    live: [],
    history: { items: [], total: 0, limit: 20, offset: 0 },
  });
  vi.mocked(fetchLicenceStatusHistory).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
  vi.mocked(fetchLicenceBindingHistory).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
  vi.mocked(fetchLicenceVerificationAttempts).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
  vi.mocked(fetchAuditEvents).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
});

describe('AdminLicenceDetailRoute', () => {
  it('renders a loading skeleton while the licence query is pending', async () => {
    vi.mocked(fetchLicence).mockReturnValue(new Promise(() => {}));
    const { container } = renderDetail();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the licence query errors', async () => {
    vi.mocked(fetchLicence).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load this licence/i)).toBeInTheDocument();
  });

  it('renders the Licence heading and key details when loaded', async () => {
    vi.mocked(fetchLicence).mockResolvedValue(licence());
    renderDetail();
    expect(await screen.findByRole('heading', { name: /^licence$/i })).toBeInTheDocument();
    expect(screen.getByText('owner@example.com')).toBeInTheDocument();
    expect(screen.getAllByText(/not bound/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/^none$/i).length).toBeGreaterThan(0);
  });

  it('renders a link to the product detail page using productId', async () => {
    vi.mocked(fetchLicence).mockResolvedValue(licence());
    renderDetail();
    const link = await screen.findByRole('link', { name: /acme-pro/i });
    expect(link.getAttribute('href')).toBe('/admin/products/p-1');
  });

  it('shows the View order button when the licence has an orderId', async () => {
    vi.mocked(fetchLicence).mockResolvedValue(licence({ orderId: 'ord-9' }));
    renderDetail();
    const link = await screen.findByRole('link', { name: /view order/i });
    expect(link.getAttribute('href')).toBe('/admin/orders/ord-9');
  });

  it('omits the View order button when there is no associated order', async () => {
    vi.mocked(fetchLicence).mockResolvedValue(licence({ orderId: null }));
    renderDetail();
    await screen.findByRole('heading', { name: /^licence$/i });
    expect(screen.queryByRole('link', { name: /view order/i })).not.toBeInTheDocument();
  });

  it('shows the armed IP allowlist message when the allowlist is empty', async () => {
    vi.mocked(fetchLicence).mockResolvedValue(licence({ ipAllowlist: [] }));
    renderDetail();
    await screen.findByRole('heading', { name: /^licence$/i });
    expect(screen.getByText(/armed \(binds the first verifying ip\)/i)).toBeInTheDocument();
  });
});
