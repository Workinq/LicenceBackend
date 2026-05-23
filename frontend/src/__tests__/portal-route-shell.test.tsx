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

import { Route as PortalShellRoute } from '../routes/portal/route';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderShell(initialPath = '/portal') {
  const rootRoute = createRootRoute();
  const shellRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal',
    component: PortalShellRoute.options.component,
  });
  const indexRoute = createRoute({
    getParentRoute: () => shellRoute,
    path: '/',
    component: () => <div>portal-outlet</div>,
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

describe('PortalRouteShell', () => {
  it('renders the LicenceBackend wordmark and a portal badge in the header', async () => {
    renderShell();
    expect(await screen.findByText(/licencebackend/i)).toBeInTheDocument();
    expect(screen.getByText('portal')).toBeInTheDocument();
  });

  it('renders the portal sidebar with overview, licences, products and orders links', async () => {
    renderShell();
    expect(await screen.findByRole('link', { name: /overview/i })).toHaveAttribute('href', '/portal');
    expect(screen.getByRole('link', { name: /licences/i })).toHaveAttribute('href', '/portal/licences');
    expect(screen.getByRole('link', { name: /products/i })).toHaveAttribute('href', '/portal/products');
    expect(screen.getByRole('link', { name: /orders/i })).toHaveAttribute('href', '/portal/orders');
  });

  it('does not render a users link in the portal sidebar', async () => {
    renderShell();
    await screen.findByRole('link', { name: /overview/i });
    expect(screen.queryByRole('link', { name: /^users$/i })).not.toBeInTheDocument();
  });

  it('renders the child route inside the outlet', async () => {
    renderShell();
    expect(await screen.findByText('portal-outlet')).toBeInTheDocument();
  });

  it('renders the basket link in the header', async () => {
    renderShell();
    const basket = await screen.findByRole('link', { name: /basket/i });
    expect(basket).toHaveAttribute('href', '/portal/basket');
  });

  it('shows the signed-in user email in the account menu when a session exists', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'buyer@example.com',
      displayName: 'Buyer',
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderShell();
    expect(await screen.findByText('buyer@example.com')).toBeInTheDocument();
  });
});
