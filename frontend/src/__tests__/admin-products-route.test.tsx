import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/products', () => ({
  fetchProducts: vi.fn(),
  createProduct: vi.fn(),
  fetchProduct: vi.fn(),
  updateProduct: vi.fn(),
  uploadProductImage: vi.fn(),
  deleteProductImage: vi.fn(),
}));
import { fetchProducts } from '../api/products';
import { Route as ProductsRoute } from '../routes/admin/products';

type ProductRow = {
  id: string;
  slug: string;
  displayName: string;
  description: string | null;
  tagline: string | null;
  isPublic: boolean;
  price: number | null;
  currency: string;
  sortOrder: number;
  imageUrl: string | null;
  createdAt: string;
};

function product(over: Partial<ProductRow> = {}): ProductRow {
  return {
    id: 'p1',
    slug: 'acme-pro',
    displayName: 'Acme Pro',
    description: null,
    tagline: null,
    isPublic: true,
    price: null,
    currency: 'USD',
    sortOrder: 0,
    imageUrl: null,
    createdAt: '2026-01-01T00:00:00Z',
    ...over,
  };
}

function renderProducts(initial = '/admin/products') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const productsRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products',
    component: ProductsRoute.options.component,
    validateSearch: ProductsRoute.options.validateSearch,
  });
  const newRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products/new', component: () => null });
  const idRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([productsRoute, newRoute, idRoute]),
    history: createMemoryHistory({ initialEntries: [initial] }),
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

describe('AdminProductsPage (extra)', () => {
  it('renders the table view when ?view=table is in the URL', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [product({ price: 19.99 }), product({ id: 'p2', slug: 'acme-lite', displayName: 'Acme Lite', isPublic: false, imageUrl: '/products/p2.png' })],
      total: 2, limit: 25, offset: 0,
    });
    renderProducts('/admin/products?view=table');
    expect(await screen.findByRole('table')).toBeInTheDocument();
    expect(await screen.findByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('Acme Lite')).toBeInTheDocument();
  });

  it('uses the larger table page size when in table view', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [product()], total: 1, limit: 25, offset: 0 });
    renderProducts('/admin/products?view=table');
    await waitFor(() => {
      expect(vi.mocked(fetchProducts).mock.calls[0][0]).toEqual({ q: undefined, limit: 25, offset: 0 });
    });
  });

  it('switches to table view when the Table button is pressed', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [product()], total: 1, limit: 25, offset: 0 });
    renderProducts();
    await screen.findByText('Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /table view/i }));
    await waitFor(() => {
      expect(screen.getByRole('table')).toBeInTheDocument();
    });
  });

  it('shows the failure message when the query errors', async () => {
    vi.mocked(fetchProducts).mockRejectedValue(new Error('boom'));
    renderProducts();
    expect(await screen.findByText(/failed to load products/i)).toBeInTheDocument();
  });

  it('shows the search-specific empty state when filtering returns nothing', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 6, offset: 0 });
    renderProducts();
    await waitFor(() => {
      expect(vi.mocked(fetchProducts)).toHaveBeenCalled();
    });
    await userEvent.type(screen.getByPlaceholderText(/search/i), 'zzz');
    expect(await screen.findByText(/no products match your search/i)).toBeInTheDocument();
  });

  it('advances pagination in the table view and forwards the new offset', async () => {
    vi.mocked(fetchProducts)
      .mockResolvedValueOnce({ items: [product()], total: 100, limit: 25, offset: 0 })
      .mockResolvedValue({ items: [product({ id: 'p99', displayName: 'Next page' })], total: 100, limit: 25, offset: 25 });
    renderProducts('/admin/products?view=table');
    await screen.findByText('Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() => {
      expect(vi.mocked(fetchProducts).mock.calls.at(-1)?.[0]).toEqual({ q: undefined, limit: 25, offset: 25 });
    });
  });
});
