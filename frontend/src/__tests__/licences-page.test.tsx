import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/licences', () => ({
  fetchLicences: vi.fn(),
  fetchLicence: vi.fn(),
}));
import { fetchLicences } from '../api/licences';
import { Route as LicencesRoute } from '../routes/admin/licences';

function renderLicences() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const licencesRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/licences',
    component: LicencesRoute.options.component,
  });
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/licences/$id',
    component: () => null,
  });
  const newRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/licences/new',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([licencesRoute, detailRoute, newRoute]),
    history: createMemoryHistory({ initialEntries: ['/licences'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

const licence = (over: Record<string, unknown> = {}) => ({
  id: 'lic-1',
  productId: 'p-1',
  productSlug: 'acme-pro',
  userId: 'u-1',
  userEmail: 'alice@example.com',
  status: 'active',
  expiresAt: null,
  notes: null,
  hwidBound: false,
  ipAllowlist: null,
  createdAt: '2026-01-01T00:00:00Z',
  ...over,
});

beforeEach(() => {
  vi.mocked(fetchLicences).mockReset();
});

describe('LicencesPage', () => {
  it('renders a row per licence with the product slug, user email, and status', async () => {
    vi.mocked(fetchLicences).mockResolvedValue({
      items: [licence(), licence({ id: 'lic-2', productSlug: 'acme-lite', userEmail: 'bob@example.com', status: 'revoked' })],
      total: 2,
      limit: 25,
      offset: 0,
    });
    renderLicences();
    expect(await screen.findByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('acme-lite')).toBeInTheDocument();
    expect(screen.getByText('revoked')).toBeInTheDocument();
  });

  it('has a New licence link pointing at /licences/new', async () => {
    vi.mocked(fetchLicences).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderLicences();
    const link = await screen.findByRole('link', { name: /new licence/i });
    expect(link).toHaveAttribute('href', '/admin/licences/new');
  });

  it('shows an empty-state message when there are no licences', async () => {
    vi.mocked(fetchLicences).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderLicences();
    expect(await screen.findByText(/no licences/i)).toBeInTheDocument();
  });

  it('shows an error message when the query fails', async () => {
    vi.mocked(fetchLicences).mockRejectedValue(new Error('boom'));
    renderLicences();
    expect(await screen.findByText(/failed to load/i)).toBeInTheDocument();
  });
});
