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

vi.mock('../api/products', () => ({
  fetchProducts: vi.fn(),
  createProduct: vi.fn(),
  fetchProduct: vi.fn(),
  updateProduct: vi.fn(),
  uploadProductImage: vi.fn(),
  deleteProductImage: vi.fn(),
}));
import { fetchProducts } from '../api/products';
import { Route as ProductsRoute } from '../routes/_authed/products';

type ProductResponse = {
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

function product(over: Partial<ProductResponse> = {}): ProductResponse {
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

function renderProducts() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const productsRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/products',
    component: ProductsRoute.options.component,
  });
  const newRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/products/new',
    component: () => null,
  });
  const idRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/products/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([productsRoute, newRoute, idRoute]),
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
  it('renders a card per product with its name, slug, and visibility', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [
        product(),
        product({ id: 'p2', slug: 'acme-lite', displayName: 'Acme Lite', isPublic: false }),
      ],
      total: 2,
      limit: 200,
      offset: 0,
    });
    renderProducts();
    expect(await screen.findByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByText('Acme Lite')).toBeInTheDocument();
    expect(screen.getByText('acme-lite')).toBeInTheDocument();
    expect(screen.getByText('Public')).toBeInTheDocument();
    expect(screen.getByText('Private')).toBeInTheDocument();
  });

  it('has a New product link pointing at /products/new', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    renderProducts();
    expect((await screen.findByRole('link', { name: /new product/i })).getAttribute('href')).toBe(
      '/products/new',
    );
  });

  it('filters the cards by display name via the search box', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [
        product({ id: 'p1', displayName: 'Acme Pro' }),
        product({ id: 'p2', slug: 'acme-lite', displayName: 'Acme Lite' }),
      ],
      total: 2,
      limit: 200,
      offset: 0,
    });
    renderProducts();
    await screen.findByText('Acme Pro');
    await userEvent.type(screen.getByPlaceholderText(/search/i), 'pro');
    expect(screen.getByText('Acme Pro')).toBeInTheDocument();
    expect(screen.queryByText('Acme Lite')).not.toBeInTheDocument();
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
