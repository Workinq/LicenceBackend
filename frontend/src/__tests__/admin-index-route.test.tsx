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

vi.mock('../api/licences', () => ({
  fetchLicences: vi.fn().mockResolvedValue({ items: [], total: 0, limit: 1, offset: 0 }),
}));
vi.mock('../api/products', () => ({
  fetchProducts: vi.fn().mockResolvedValue({ items: [], total: 0, limit: 1, offset: 0 }),
}));
vi.mock('../api/audit-events', () => ({
  fetchAuditEvents: vi.fn().mockResolvedValue({ items: [], total: 0, limit: 7, offset: 0 }),
}));

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

describe('AdminIndexRoute', () => {
  it('shows the overview heading and metric labels', async () => {
    renderAdminIndex();
    expect(await screen.findByRole('heading', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getAllByText(/active licences/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/revoked licences/i)).toBeInTheDocument();
    expect(screen.getByText(/total licences/i)).toBeInTheDocument();
  });

  it('renders the recent activity feed section', async () => {
    renderAdminIndex();
    expect(await screen.findByText(/recent activity/i)).toBeInTheDocument();
    expect(screen.getByText(/top products/i)).toBeInTheDocument();
  });
});
