import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/me-licences', () => ({ fetchMyLicences: vi.fn() }));
import { fetchMyLicences } from '../api/me-licences';
import { Route as PortalLicencesRoute } from '../routes/portal/licences';

type LicenceItem = {
  id: string;
  productId: string;
  productSlug: string;
  userId: string;
  userEmail: string;
  status: string;
  expiresAt: string | null;
  notes: string | null;
  hwidBound: boolean;
  hasKey: boolean;
  ipAllowlist: string[] | null;
  label: string | null;
  createdAt: string;
  relationship: string | null;
};

function licence(over: Partial<LicenceItem> = {}): LicenceItem {
  return {
    id: 'lic-1',
    productId: 'p1',
    productSlug: 'acme-pro',
    userId: 'u1',
    userEmail: 'me@example.com',
    status: 'active',
    expiresAt: null,
    notes: null,
    hwidBound: false,
    hasKey: true,
    ipAllowlist: null,
    label: null,
    createdAt: '2026-01-01T00:00:00Z',
    relationship: 'owner',
    ...over,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const listRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/licences',
    component: PortalLicencesRoute.options.component,
  });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/licences/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([listRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/licences'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyLicences).mockReset();
});

describe('PortalLicencesPage', () => {
  it('renders rows for each licence with product slug, status and HWID indicator', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({
      items: [
        licence({ id: 'lic-1', productSlug: 'acme-pro', hwidBound: true, label: 'My Mac' }),
        licence({ id: 'lic-2', productSlug: 'acme-lite', status: 'suspended', relationship: 'member' }),
      ],
      total: 2,
      limit: 25,
      offset: 0,
    });
    renderPage();
    expect(await screen.findByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByText('acme-lite')).toBeInTheDocument();
    expect(screen.getByText('My Mac')).toBeInTheDocument();
    expect(screen.getByText(/bound/i)).toBeInTheDocument();
  });

  it('shows the empty state when there are no licences', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderPage();
    expect(await screen.findByText(/do not have any licences yet/i)).toBeInTheDocument();
  });

  it('shows an error row when the query fails', async () => {
    vi.mocked(fetchMyLicences).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText(/failed to load your licences/i)).toBeInTheDocument();
  });

  it('disables Previous on the first page and enables Next when more pages exist', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({
      items: [licence()],
      total: 100,
      limit: 25,
      offset: 0,
    });
    renderPage();
    await screen.findByText('acme-pro');
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).toBeEnabled();
  });

  it('advances the offset when Next is clicked', async () => {
    vi.mocked(fetchMyLicences)
      .mockResolvedValueOnce({ items: [licence()], total: 100, limit: 25, offset: 0 })
      .mockResolvedValue({ items: [licence({ id: 'lic-2', productSlug: 'page-2' })], total: 100, limit: 25, offset: 25 });
    renderPage();
    await screen.findByText('acme-pro');
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    expect(await screen.findByText('page-2')).toBeInTheDocument();
    expect(vi.mocked(fetchMyLicences).mock.calls.at(-1)?.[0]).toEqual({ limit: 25, offset: 25 });
  });
});
