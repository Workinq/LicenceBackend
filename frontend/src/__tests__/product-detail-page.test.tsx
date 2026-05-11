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

vi.mock('../api/products', () => ({
  fetchProduct: vi.fn(),
  updateProduct: vi.fn(),
  uploadProductImage: vi.fn(),
  deleteProductImage: vi.fn(),
  fetchProducts: vi.fn(),
  createProduct: vi.fn(),
}));
import { fetchProduct } from '../api/products';
import { Route as ProductDetailRoute } from '../routes/_authed/products_.$id';

function makeProduct(over: Record<string, unknown> = {}) {
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

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products/$id', component: ProductDetailRoute.options.component });
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute, listRoute]),
    history: createMemoryHistory({ initialEntries: ['/products/p1'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProduct).mockReset();
});

describe('ProductDetailPage', () => {
  it('renders the product display name and slug and an edit form', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    expect(await screen.findByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/upload product image/i)).toBeInTheDocument();
  });

  it('pre-fills the edit form from the product', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ displayName: 'Acme Pro', tagline: 'The pro one', currency: 'EUR' }));
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(screen.getByLabelText(/display name/i)).toHaveValue('Acme Pro');
    expect(screen.getByLabelText(/tagline/i)).toHaveValue('The pro one');
    expect(screen.getByLabelText(/currency/i)).toHaveValue('EUR');
  });

  it('shows the placeholder and no Remove image button when there is no image', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ imageUrl: null }));
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(screen.queryByRole('button', { name: /remove image/i })).not.toBeInTheDocument();
  });

  it('shows the image and a Remove image button when there is an image', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ imageUrl: '/products/p1/image' }));
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(screen.getByRole('button', { name: /remove image/i })).toBeInTheDocument();
    const img = document.querySelector('img');
    expect(img?.getAttribute('src')).toContain('/api/products/p1/image');
  });
});
