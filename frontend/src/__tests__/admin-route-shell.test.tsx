import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

vi.mock('../auth/use-silent-refresh', () => ({ useSilentRefresh: vi.fn() }));
vi.mock('../auth/api-client', () => ({
  API_BASE: '/api',
  apiClient: vi.fn().mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 }, status: 200, headers: new Headers() }),
  authedFetch: vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'ok', db: 'ok', version: '0.0.0' }), { status: 200 })),
}));

import { Route as AdminShellRoute } from '../routes/admin/route';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderShell(initialPath = '/admin') {
  const rootRoute = createRootRoute();
  const shellRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin',
    component: AdminShellRoute.options.component,
  });
  const indexRoute = createRoute({
    getParentRoute: () => shellRoute,
    path: '/',
    component: () => <div>admin-outlet</div>,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([shellRoute.addChildren([indexRoute])]),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('AdminRouteShell', () => {
  it('renders the LicenceBackend brand in the header', async () => {
    renderShell();
    expect(await screen.findByText(/licencebackend/i)).toBeInTheDocument();
  });

  it('renders the sidebar nav with overview, licences, products, orders and users', async () => {
    renderShell();
    expect(await screen.findByRole('link', { name: /overview/i })).toHaveAttribute('href', '/admin');
    expect(screen.getByRole('link', { name: /licences/i })).toHaveAttribute('href', '/admin/licences');
    expect(screen.getByRole('link', { name: /products/i })).toHaveAttribute('href', '/admin/products');
    expect(screen.getByRole('link', { name: /orders/i })).toHaveAttribute('href', '/admin/orders');
    expect(screen.getByRole('link', { name: /users/i })).toHaveAttribute('href', '/admin/users');
  });

  it('renders the child route inside the outlet', async () => {
    renderShell();
    expect(await screen.findByText('admin-outlet')).toBeInTheDocument();
  });

  it('shows the signed-in user email in the account menu when a session exists', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'admin@example.com',
      displayName: 'Admin',
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderShell();
    expect(await screen.findByText('admin@example.com')).toBeInTheDocument();
  });

  it('falls back to Account placeholder when there is no user', async () => {
    renderShell();
    expect(await screen.findByText(/^account$/i)).toBeInTheDocument();
  });
});
