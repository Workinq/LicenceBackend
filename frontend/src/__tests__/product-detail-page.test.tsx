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
vi.mock('../api/product-files', () => ({
  fetchProductFiles: vi.fn(),
  uploadProductFile: vi.fn(),
  downloadProductFileRevision: vi.fn(),
  triggerBlobDownload: vi.fn(),
}));
import { fetchProduct } from '../api/products';
import { fetchProductFiles, uploadProductFile } from '../api/product-files';
import { Route as ProductDetailRoute } from '../routes/admin/products_.$id';

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
  vi.mocked(fetchProductFiles).mockReset();
  vi.mocked(uploadProductFile).mockReset();
  vi.mocked(fetchProductFiles).mockResolvedValue([]);
});

describe('ProductDetailPage', () => {
  it('renders the product display name and slug and an edit form', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    expect(await screen.findByText('Acme Pro')).toBeInTheDocument();
    expect(screen.getByText('acme-pro')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/upload image/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^cancel$/i })).toBeInTheDocument();
  });

  it('pre-fills the edit form from the product', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ displayName: 'Acme Pro', tagline: 'The pro one', currency: 'EUR' }));
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(screen.getByLabelText(/display name/i)).toHaveValue('Acme Pro');
    expect(screen.getByLabelText(/tagline/i)).toHaveValue('The pro one');
    expect(await screen.findByRole('combobox', { name: /EUR/i })).toBeInTheDocument();
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

  it('shows an empty state in Downloads when no revisions exist', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchProductFiles).mockResolvedValue([]);
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(await screen.findByText(/no revisions uploaded yet/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/upload first revision/i)).toBeInTheDocument();
  });

  it('lists revisions newest-first with version and filename', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchProductFiles).mockResolvedValue([
      {
        id: 'f2', productId: 'p1', versionNumber: 2, fileName: 'new.zip',
        contentType: 'application/zip', fileSizeBytes: 2048,
        uploadedByAdminId: 'a1', uploadedAt: '2026-05-01T00:00:00Z',
      },
      {
        id: 'f1', productId: 'p1', versionNumber: 1, fileName: 'old.zip',
        contentType: 'application/zip', fileSizeBytes: 1024,
        uploadedByAdminId: 'a1', uploadedAt: '2026-04-01T00:00:00Z',
      },
    ]);
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(await screen.findByText(/v2/)).toBeInTheDocument();
    expect(screen.getByText(/v1/)).toBeInTheDocument();
    expect(screen.getByText(/latest/i)).toBeInTheDocument();
    expect(screen.getByText(/new\.zip/)).toBeInTheDocument();
    expect(screen.getByText(/old\.zip/)).toBeInTheDocument();
    expect(screen.getByLabelText(/upload new revision/i)).toBeInTheDocument();
  });
});
