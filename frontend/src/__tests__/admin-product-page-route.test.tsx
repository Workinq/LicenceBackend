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
}));
vi.mock('../api/product-content-images', () => ({
  uploadProductContentImage: vi.fn(),
}));
vi.mock('../components/products/ProductPageEditor', () => ({
  ProductPageEditor: () => <div data-testid="page-editor">page-editor</div>,
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { fetchProduct } from '../api/products';
import { Route as PageEditRoute } from '../routes/admin/products_.$id.page';

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
    pageContent: null,
    ...over,
  };
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const editRoute = PageEditRoute.update({
    id: '/admin/products_/$id/page',
    path: '/admin/products/$id/page',
    getParentRoute: () => rootRoute,
  } as never);
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([editRoute as never, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/products/p1/page'] }),
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

describe('AdminProductPageEditRoute', () => {
  it('renders a loading skeleton while the product query is pending', async () => {
    vi.mocked(fetchProduct).mockReturnValue(new Promise(() => {}));
    const { container } = renderPage();
    await vi.waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the product query errors', async () => {
    vi.mocked(fetchProduct).mockRejectedValue(new Error('boom'));
    renderPage();
    expect(await screen.findByText(/failed to load this product/i)).toBeInTheDocument();
  });

  it('renders the product display name as a page heading once loaded', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ displayName: 'Acme Pro' }));
    renderPage();
    expect(
      await screen.findByRole('heading', { name: /acme pro - page content/i }),
    ).toBeInTheDocument();
  });

  it('renders the back-to-product link with the product id', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderPage();
    const link = await screen.findByRole('link', { name: /back to product/i });
    expect(link.getAttribute('href')).toBe('/admin/products/p1');
  });

  it('mounts the ProductPageEditor child component on success', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderPage();
    expect(await screen.findByTestId('page-editor')).toBeInTheDocument();
  });
});
