import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/products', () => ({
  fetchProduct: vi.fn(),
  fetchProducts: vi.fn(),
  updateProduct: vi.fn(),
  createProduct: vi.fn(),
  uploadProductImage: vi.fn(),
  deleteProductImage: vi.fn(),
}));
vi.mock('../components/products/ProductPageContent', () => ({
  ProductPageContent: () => <div data-testid="page-content">page-content</div>,
}));

import { fetchProduct } from '../api/products';
import { Route as PortalProductDetailRoute } from '../routes/portal/products_.$id';
import type { ProductResponse } from '../api/generated/api.schemas';

function product(over: Partial<ProductResponse> = {}): ProductResponse {
  return {
    id: 'p-1',
    slug: 'acme-pro',
    displayName: 'Acme Pro',
    description: null,
    tagline: null,
    isPublic: true,
    price: 19.99,
    currency: 'USD',
    sortOrder: 0,
    imageUrl: null,
    createdAt: '2026-01-01T00:00:00Z',
    pageContent: null,
    ...over,
  };
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = PortalProductDetailRoute.update({
    id: '/portal/products_/$id',
    path: '/portal/products/$id',
    getParentRoute: () => rootRoute,
  } as never);
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute as never, listRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/products/p-1'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProduct).mockReset();
});

describe('PortalProductDetailRoute', () => {
  it('renders a loading skeleton while the product query is pending', async () => {
    vi.mocked(fetchProduct).mockReturnValue(new Promise(() => {}));
    const { container } = renderDetail();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the product query errors', async () => {
    vi.mocked(fetchProduct).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load this product/i)).toBeInTheDocument();
  });

  it('renders the product display name, tagline and price', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(product({ tagline: 'Best in class' }));
    renderDetail();
    expect(await screen.findByRole('heading', { name: /acme pro/i })).toBeInTheDocument();
    expect(screen.getByText(/best in class/i)).toBeInTheDocument();
    expect(screen.getByText(/\$19\.99/)).toBeInTheDocument();
  });

  it('renders Free when the product has no price', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(product({ price: null }));
    renderDetail();
    expect(await screen.findByText(/^free$/i)).toBeInTheDocument();
  });

  it('renders a back-to-products link', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(product());
    renderDetail();
    const link = await screen.findByRole('link', { name: /back to products/i });
    expect(link.getAttribute('href')).toBe('/portal/products');
  });

  it('renders the product image when imageUrl is set and omits page content when null', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(product({ imageUrl: '/products/p-1/image' }));
    renderDetail();
    await screen.findByRole('heading', { name: /acme pro/i });
    const img = document.querySelector('img');
    expect(img?.getAttribute('src')).toBe('/api/products/p-1/image');
    expect(screen.queryByTestId('page-content')).not.toBeInTheDocument();
  });

  it('renders the page content component when the product has pageContent', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(
      product({ pageContent: { type: 'doc', content: [] } as never }),
    );
    renderDetail();
    expect(await screen.findByTestId('page-content')).toBeInTheDocument();
  });
});
