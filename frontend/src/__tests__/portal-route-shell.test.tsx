import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../auth/use-silent-refresh', () => ({ useSilentRefresh: vi.fn() }));
vi.mock('../auth/api-client', () => ({
  API_BASE: '/api',
  apiClient: vi.fn().mockResolvedValue(undefined),
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
  render(<RouterProvider router={router} />);
}

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('PortalRouteShell', () => {
  it('renders the My account header brand', async () => {
    renderShell();
    expect(await screen.findByText(/my account/i)).toBeInTheDocument();
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
