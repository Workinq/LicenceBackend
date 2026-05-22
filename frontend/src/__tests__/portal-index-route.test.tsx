import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/me-licences', () => ({ fetchMyLicences: vi.fn() }));
vi.mock('../api/products', () => ({ fetchProducts: vi.fn() }));

import { fetchMyLicences } from '../api/me-licences';
import { fetchProducts } from '../api/products';
import { Route as PortalIndexRoute } from '../routes/portal/index';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderPortalIndex() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal',
    component: PortalIndexRoute.options.component,
  });
  const licencesRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/licences', component: () => null });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, licencesRoute, productsRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyLicences).mockReset();
  vi.mocked(fetchProducts).mockReset();
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id: 'u1',
    email: 'user@example.com',
    displayName: 'Alex',
    role: 'user',
    status: 'active',
    createdAt: '2026-01-01T00:00:00Z',
  });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('PortalOverview', () => {
  it('greets the user by display name and renders summary cards', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({ items: [], total: 3, limit: 1, offset: 0 });
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 7, limit: 1, offset: 0 });
    renderPortalIndex();
    expect(await screen.findByRole('heading', { name: /welcome, alex/i })).toBeInTheDocument();
    expect(screen.getByText('Your licences')).toBeInTheDocument();
    expect(screen.getByText('Browse products')).toBeInTheDocument();
    expect(await screen.findByText('3')).toBeInTheDocument();
    expect(await screen.findByText('7')).toBeInTheDocument();
  });

  it('links to the licences and products pages', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({ items: [], total: 0, limit: 1, offset: 0 });
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 1, offset: 0 });
    renderPortalIndex();
    const viewAll = await screen.findByRole('link', { name: /view all/i });
    const openCatalog = await screen.findByRole('link', { name: /open catalog/i });
    expect(viewAll).toHaveAttribute('href', '/portal/licences');
    expect(openCatalog).toHaveAttribute('href', '/portal/products');
  });

  it('shows a failure message when a summary query errors', async () => {
    vi.mocked(fetchMyLicences).mockRejectedValue(new Error('boom'));
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 2, limit: 1, offset: 0 });
    renderPortalIndex();
    expect(await screen.findByText(/failed to load\./i)).toBeInTheDocument();
  });
});
