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

vi.mock('../api/products', () => ({ fetchProducts: vi.fn(), createProduct: vi.fn() }));
import { fetchProducts } from '../api/products';
import { Route as ProductsRoute } from '../routes/_authed/products';

function renderProducts() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products', component: ProductsRoute.options.component });
  const newRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products/new', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([productsRoute, newRoute]),
    history: createMemoryHistory({ initialEntries: ['/products'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProducts).mockReset();
});

describe('ProductsPage', () => {
  it('renders a row per product with the slug and display name', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [
        { id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', createdAt: '2026-01-01T00:00:00Z' },
        { id: 'p2', slug: 'acme-lite', displayName: 'Acme Lite', createdAt: '2026-01-02T00:00:00Z' },
      ],
      total: 2, limit: 200, offset: 0,
    });
    renderProducts();
    expect(await screen.findByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('acme-lite')).toBeInTheDocument();
    expect(screen.getByText('Acme Lite')).toBeInTheDocument();
  });

  it('has a New product link pointing at /products/new', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    renderProducts();
    const link = await screen.findByRole('link', { name: /new product/i });
    expect(link).toHaveAttribute('href', '/products/new');
  });

  it('shows an empty-state message when there are no products', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    renderProducts();
    expect(await screen.findByText(/no products yet/i)).toBeInTheDocument();
  });

  it('shows an error message when the query fails', async () => {
    vi.mocked(fetchProducts).mockRejectedValue(new Error('boom'));
    renderProducts();
    expect(await screen.findByText(/failed to load/i)).toBeInTheDocument();
  });
});
