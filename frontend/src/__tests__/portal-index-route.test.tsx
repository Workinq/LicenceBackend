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

import { fetchMyLicences } from '../api/me-licences';
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
  const ordersRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/orders', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, licencesRoute, productsRoute, ordersRoute]),
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
  it('greets the user by display name and renders metric cards', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({ items: [], total: 3, limit: 50, offset: 0 });
    renderPortalIndex();
    expect(await screen.findByRole('heading', { name: /welcome, alex/i })).toBeInTheDocument();
    expect(screen.getByText(/active licences/i)).toBeInTheDocument();
    expect(screen.getByText(/devices bound/i)).toBeInTheDocument();
    expect(screen.getByText(/next renewal/i)).toBeInTheDocument();
  });

  it('renders the Your licences section and quick action links', async () => {
    vi.mocked(fetchMyLicences).mockResolvedValue({ items: [], total: 0, limit: 50, offset: 0 });
    renderPortalIndex();
    expect(await screen.findByRole('heading', { name: /your licences/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view all/i })).toHaveAttribute('href', '/portal/licences');
    expect(screen.getByText(/browse catalogue/i)).toBeInTheDocument();
  });
});
