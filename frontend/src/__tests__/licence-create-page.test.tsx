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

vi.mock('../api/products', () => ({ fetchProducts: vi.fn() }));
vi.mock('../api/users', () => ({ fetchUsers: vi.fn() }));
vi.mock('../api/licences', () => ({ createLicence: vi.fn(), fetchLicences: vi.fn(), fetchLicence: vi.fn() }));
import { fetchProducts } from '../api/products';
import { fetchUsers } from '../api/users';
import { Route as NewLicenceRoute } from '../routes/_authed/licences_.new';

function renderNew() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const newRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences/new', component: NewLicenceRoute.options.component });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences/$id', component: () => null });
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences', component: () => null });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([newRoute, detailRoute, listRoute, productsRoute]),
    history: createMemoryHistory({ initialEntries: ['/licences/new'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProducts).mockReset();
  vi.mocked(fetchUsers).mockReset();
});

describe('NewLicencePage', () => {
  it('renders the create form with product and user fields once the lists load', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    renderNew();
    expect(await screen.findByRole('button', { name: /create licence/i })).toBeInTheDocument();
    expect(screen.getByText(/product/i)).toBeInTheDocument();
    expect(screen.getByText(/user/i)).toBeInTheDocument();
  });

  it('shows a message when there are no products', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    renderNew();
    expect(await screen.findByText(/no products|there are no products/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create licence/i })).toBeInTheDocument();
  });

  it('shows a validation error when submitting without a product', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    renderNew();
    const submitBtn = await screen.findByRole('button', { name: /create licence/i });
    await userEvent.click(submitBtn);
    expect(await screen.findByText(/choose a product/i)).toBeInTheDocument();
  });
});
