import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { Route as AdminIndexRoute } from '../routes/admin/index';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderAdminIndex() {
  const rootRoute = createRootRoute();
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin',
    component: AdminIndexRoute.options.component,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin'] }),
  });
  render(<RouterProvider router={router} />);
}

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('AdminIndexRoute', () => {
  it('shows the overview heading and the signed-in user email and role', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'admin@example.com',
      displayName: 'Admin',
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderAdminIndex();
    expect(await screen.findByRole('heading', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByText('admin@example.com')).toBeInTheDocument();
    expect(screen.getByText(/\(admin\)/)).toBeInTheDocument();
  });

  it('shows a loading session message when there is no signed-in user', async () => {
    renderAdminIndex();
    expect(await screen.findByText(/loading session/i)).toBeInTheDocument();
  });
});
