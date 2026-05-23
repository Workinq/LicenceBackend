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
  createProduct: vi.fn(),
  uploadProductImage: vi.fn(),
  fetchProducts: vi.fn(),
  fetchProduct: vi.fn(),
  updateProduct: vi.fn(),
  deleteProductImage: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { createProduct, uploadProductImage } from '../api/products';
import { ApiError } from '../auth/api-client';
import { Route as NewProductRoute } from '../routes/admin/products_.new';
import type { ProductResponse } from '../api/generated/api.schemas';

function makeProduct(over: Partial<ProductResponse> = {}): ProductResponse {
  return {
    id: 'p-new',
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

function renderNew() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const newRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products/new',
    component: NewProductRoute.options.component,
  });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products', component: () => null });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products/$id', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([newRoute, productsRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/products/new'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

const swallow = () => {};

beforeEach(() => {
  vi.mocked(createProduct).mockReset();
  vi.mocked(uploadProductImage).mockReset();
  process.on('unhandledRejection', swallow);
  if (typeof window !== 'undefined') {
    window.addEventListener('unhandledrejection', swallow);
  }
});

afterEach(() => {
  process.off('unhandledRejection', swallow);
  if (typeof window !== 'undefined') {
    window.removeEventListener('unhandledrejection', swallow);
  }
});

describe('AdminProductNewRoute', () => {
  it('renders the New product heading with slug and display name fields', async () => {
    renderNew();
    expect(await screen.findByRole('heading', { name: /new product/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^slug$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create product/i })).toBeInTheDocument();
  });

  it('shows a slug format error when the slug contains invalid characters', async () => {
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'Bad Slug');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(
      await screen.findByText(/lowercase letters, numbers, and hyphens only/i),
    ).toBeInTheDocument();
    expect(vi.mocked(createProduct)).not.toHaveBeenCalled();
  });

  it('shows the display name required error when display name is empty on submit', async () => {
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'acme-pro');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(await screen.findByText(/display name is required/i)).toBeInTheDocument();
    expect(vi.mocked(createProduct)).not.toHaveBeenCalled();
  });

  it('submits the form values converted from strings to numbers', async () => {
    vi.mocked(createProduct).mockResolvedValue(makeProduct());
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'acme-pro');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme Pro');
    await userEvent.type(screen.getByLabelText(/^price$/i), '9.99');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));

    await vi.waitFor(() => {
      expect(vi.mocked(createProduct)).toHaveBeenCalledTimes(1);
    });
    expect(vi.mocked(createProduct).mock.calls[0][0]).toMatchObject({
      slug: 'acme-pro',
      displayName: 'Acme Pro',
      price: 9.99,
      currency: 'USD',
      isPublic: true,
      sortOrder: 0,
    });
  });

  it('shows the API error detail when createProduct fails', async () => {
    vi.mocked(createProduct).mockRejectedValue(new ApiError(409, { detail: 'Slug already exists.' }));
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'acme-pro');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(await screen.findByText(/slug already exists/i)).toBeInTheDocument();
  });

  it('falls back to a generic error message when the failure is not an ApiError', async () => {
    vi.mocked(createProduct).mockRejectedValue(new Error('boom'));
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^slug$/i), 'acme-pro');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme Pro');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(await screen.findByText(/could not create the product/i)).toBeInTheDocument();
  });
});
