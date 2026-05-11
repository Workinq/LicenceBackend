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

vi.mock('../api/products', () => ({ createProduct: vi.fn(), fetchProducts: vi.fn(), fetchProduct: vi.fn(), updateProduct: vi.fn(), uploadProductImage: vi.fn(), deleteProductImage: vi.fn() }));
import { createProduct } from '../api/products';
import { Route as NewProductRoute } from '../routes/_authed/products_.new';

function renderNew() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const newRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products/new', component: NewProductRoute.options.component });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([newRoute, productsRoute]),
    history: createMemoryHistory({ initialEntries: ['/products/new'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(createProduct).mockReset();
});

describe('NewProductPage', () => {
  it('renders the create form with slug and display name fields', async () => {
    renderNew();
    expect(await screen.findByRole('button', { name: /create product/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^slug$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/tagline/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^price$/i)).toBeInTheDocument();
    expect(screen.getByText('Currency')).toBeInTheDocument();
    expect(screen.getByLabelText(/sort order/i)).toBeInTheDocument();
    expect(screen.getByRole('switch')).toBeInTheDocument();
    expect(screen.getByText(/choose image/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^cancel$/i })).toBeInTheDocument();
  });

  it('submits slug and display name to createProduct', async () => {
    vi.mocked(createProduct).mockResolvedValue({ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' });
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'acme-pro');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(vi.mocked(createProduct)).toHaveBeenCalledWith(expect.objectContaining({ slug: 'acme-pro', displayName: 'Acme Pro', isPublic: true }));
  });

  it('shows a validation error when slug is empty on submit', async () => {
    renderNew();
    await userEvent.type(await screen.findByLabelText(/display name/i), 'Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(await screen.findByText(/slug/i)).toBeInTheDocument();
    expect(vi.mocked(createProduct)).not.toHaveBeenCalled();
  });
});
