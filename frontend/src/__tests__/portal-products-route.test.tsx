import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
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
}));
import { fetchProducts } from '../api/products';
import { Route as PortalProductsRoute } from '../routes/portal/products';
import { useBasketStore } from '../state/basket-store';
import { useAccessTokenStore } from '../auth/access-token-store';
import type { ProductResponse } from '../api/generated/api.schemas';

function product(over: Partial<ProductResponse> = {}): ProductResponse {
  return {
    id: 'p1',
    slug: 'widget',
    displayName: 'Widget',
    description: null,
    tagline: null,
    isPublic: true,
    price: 1000,
    currency: 'USD',
    sortOrder: 0,
    imageUrl: null,
    createdAt: '2026-01-01T00:00:00Z',
    pageContent: null,
    ...over,
  } as ProductResponse;
}

function renderProducts(initialPath = '/portal/products') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const productsRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/products',
    component: PortalProductsRoute.options.component,
    validateSearch: PortalProductsRoute.options.validateSearch,
  });
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/products/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([productsRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProducts).mockReset();
  window.localStorage.clear();
  useBasketStore.setState({ items: [] });
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id: 'u1',
    email: 'u1@example.com',
    displayName: null,
    role: 'user',
    status: 'active',
    createdAt: '2026-01-01T00:00:00Z',
  });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
  window.localStorage.clear();
});

describe('PortalProductsRoute', () => {
  it('renders the catalog header and the empty state when there are no products', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 6, offset: 0 });
    renderProducts();
    expect(await screen.findByRole('heading', { name: /products/i })).toBeInTheDocument();
    expect(await screen.findByText(/no products are available yet/i)).toBeInTheDocument();
  });

  it('renders a card per product with name, slug, and price in cards view', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [
        product(),
        product({ id: 'p2', slug: 'gizmo', displayName: 'Gizmo', price: null }),
      ],
      total: 2,
      limit: 6,
      offset: 0,
    });
    renderProducts();
    expect((await screen.findAllByText('Widget')).length).toBeGreaterThan(0);
    expect(screen.getByText('widget')).toBeInTheDocument();
    expect(screen.getAllByText('Gizmo').length).toBeGreaterThan(0);
    expect(screen.getByText('gizmo')).toBeInTheDocument();
    expect(screen.getByText('Free')).toBeInTheDocument();
  });

  it('shows an error message when the query fails', async () => {
    vi.mocked(fetchProducts).mockRejectedValue(new Error('boom'));
    renderProducts();
    expect(await screen.findByText(/failed to load products/i)).toBeInTheDocument();
  });

  it('switching to table view re-renders with the table headers', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [product()],
      total: 1,
      limit: 6,
      offset: 0,
    });
    renderProducts();
    await screen.findByText('Widget');
    await userEvent.click(screen.getByRole('button', { name: /table view/i }));
    expect(await screen.findByRole('columnheader', { name: /^name$/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /^slug$/i })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /^price$/i })).toBeInTheDocument();
  });

  it('clicking Add to basket pushes the product into the basket store', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [product()],
      total: 1,
      limit: 6,
      offset: 0,
    });
    renderProducts();
    await screen.findByText('Widget');
    await userEvent.click(screen.getByRole('button', { name: /add to basket/i }));
    expect(useBasketStore.getState().items).toHaveLength(1);
    expect(useBasketStore.getState().items[0].productId).toBe('p1');
  });

  it('disables Previous on the first page and enables Next when there are more items', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: Array.from({ length: 6 }, (_, i) => product({ id: `p${i}`, slug: `s${i}`, displayName: `P${i}` })),
      total: 12,
      limit: 6,
      offset: 0,
    });
    renderProducts();
    await screen.findAllByText('P0');
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).not.toBeDisabled();
  });

  it('renders the range label reflecting the current page and total', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({
      items: [product()],
      total: 1,
      limit: 6,
      offset: 0,
    });
    renderProducts();
    await screen.findByText('Widget');
    expect(screen.getByText('1-1 of 1')).toBeInTheDocument();
  });
});
