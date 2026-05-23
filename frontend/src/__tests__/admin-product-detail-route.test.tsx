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
vi.mock('../api/licences', () => ({
  fetchLicences: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import {
  fetchProduct,
  updateProduct,
  uploadProductImage,
  deleteProductImage,
} from '../api/products';
import {
  fetchProductFiles,
  uploadProductFile,
  downloadProductFileRevision,
  triggerBlobDownload,
} from '../api/product-files';
import { fetchLicences } from '../api/licences';
import { ApiError } from '../auth/api-client';
import { toast } from 'sonner';
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
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products/$id',
    component: ProductDetailRoute.options.component,
  });
  const listRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products',
    component: () => null,
  });
  const pageRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/products/$id/page',
    component: () => null,
  });
  const licenceRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/licences/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute, listRoute, pageRoute, licenceRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/products/p1'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

const swallow = () => {};

beforeEach(() => {
  vi.mocked(fetchProduct).mockReset();
  vi.mocked(updateProduct).mockReset();
  vi.mocked(uploadProductImage).mockReset();
  vi.mocked(deleteProductImage).mockReset();
  vi.mocked(fetchProductFiles).mockReset();
  vi.mocked(uploadProductFile).mockReset();
  vi.mocked(downloadProductFileRevision).mockReset();
  vi.mocked(triggerBlobDownload).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
  vi.mocked(fetchProductFiles).mockResolvedValue([]);
  vi.mocked(fetchLicences).mockReset();
  vi.mocked(fetchLicences).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
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

describe('AdminProductDetailRoute', () => {
  it('renders a loading skeleton while the product query is pending', async () => {
    vi.mocked(fetchProduct).mockReturnValue(new Promise(() => {}));
    renderDetail();
    await vi.waitFor(() => {
      expect(document.querySelector('[data-slot="skeleton"]')).toBeTruthy();
    });
  });

  it('shows a failure message when the product query errors', async () => {
    vi.mocked(fetchProduct).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load this product/i)).toBeInTheDocument();
  });

  it('submits the edited form values to updateProduct', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ price: 9.99, sortOrder: 3, tagline: 'Old tag' }));
    vi.mocked(updateProduct).mockResolvedValue(makeProduct({ tagline: 'New tag', price: 9.99, sortOrder: 3 }));
    renderDetail();
    await screen.findByText('Acme Pro');

    const tagline = screen.getByLabelText(/tagline/i);
    await userEvent.clear(tagline);
    await userEvent.type(tagline, 'New tag');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(vi.mocked(updateProduct)).toHaveBeenCalledWith(
      'p1',
      expect.objectContaining({ tagline: 'New tag', displayName: 'Acme Pro', isPublic: true, price: 9.99, sortOrder: 3 }),
    );
  });

  it('shows the API error detail when updateProduct fails', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(updateProduct).mockRejectedValue(new ApiError(400, { detail: 'Bad input.' }));
    renderDetail();
    await screen.findByText('Acme Pro');

    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    expect(await screen.findByText(/bad input/i)).toBeInTheDocument();
  });

  it('shows a validation error when the display name is cleared on save', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    await screen.findByText('Acme Pro');

    await userEvent.clear(screen.getByLabelText(/display name/i));
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    expect(await screen.findByText(/display name is required/i)).toBeInTheDocument();
    expect(vi.mocked(updateProduct)).not.toHaveBeenCalled();
  });

  it('uploads a chosen image file via uploadProductImage', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ imageUrl: null }));
    vi.mocked(uploadProductImage).mockResolvedValue(makeProduct({ imageUrl: '/products/p1/image' }));
    renderDetail();
    await screen.findByText('Acme Pro');

    const input = document.querySelector('input[type="file"][accept^="image/"]') as HTMLInputElement;
    const file = new File(['x'], 'cover.png', { type: 'image/png' });
    await userEvent.upload(input, file);

    expect(vi.mocked(uploadProductImage)).toHaveBeenCalledWith('p1', expect.any(File));
  });

  it('removes the image via deleteProductImage when Remove image is clicked', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct({ imageUrl: '/products/p1/image' }));
    vi.mocked(deleteProductImage).mockResolvedValue(makeProduct({ imageUrl: null }));
    renderDetail();
    await screen.findByText('Acme Pro');

    await userEvent.click(await screen.findByRole('button', { name: /remove image/i }));
    expect(vi.mocked(deleteProductImage)).toHaveBeenCalledWith('p1');
  });

  it('uploads a product file revision via uploadProductFile', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchProductFiles).mockResolvedValue([]);
    vi.mocked(uploadProductFile).mockResolvedValue({
      id: 'f1',
      productId: 'p1',
      versionNumber: 1,
      fileName: 'build.zip',
      contentType: 'application/zip',
      fileSizeBytes: 1024,
      uploadedByAdminId: 'a1',
      uploadedAt: '2026-05-01T00:00:00Z',
    });
    renderDetail();
    await screen.findByText('Acme Pro');
    await screen.findByText(/no revisions uploaded yet/i);
    const fileInput = document.querySelector('input[type="file"]:not([accept^="image/"])') as HTMLInputElement;
    const file = new File(['x'], 'build.zip', { type: 'application/zip' });
    await userEvent.upload(fileInput, file);

    expect(vi.mocked(uploadProductFile)).toHaveBeenCalledWith('p1', expect.any(File));
  });

  it('downloads a revision when its download button is clicked', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchProductFiles).mockResolvedValue([
      {
        id: 'f1',
        productId: 'p1',
        versionNumber: 1,
        fileName: 'build.zip',
        contentType: 'application/zip',
        fileSizeBytes: 1024,
        uploadedByAdminId: 'a1',
        uploadedAt: '2026-05-01T00:00:00Z',
      },
    ]);
    const blob = new Blob(['x']);
    vi.mocked(downloadProductFileRevision).mockResolvedValue(blob);
    renderDetail();
    await screen.findByText('Acme Pro');

    await userEvent.click(await screen.findByRole('button', { name: /download v1/i }));
    expect(vi.mocked(downloadProductFileRevision)).toHaveBeenCalledWith('p1', 'f1');
    expect(vi.mocked(triggerBlobDownload)).toHaveBeenCalledWith(blob, 'build.zip');
  });

  it('shows an error toast when the file upload fails', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchProductFiles).mockResolvedValue([]);
    vi.mocked(uploadProductFile).mockRejectedValue(new ApiError(413, { detail: 'Too big.' }));
    renderDetail();
    await screen.findByText('Acme Pro');
    await screen.findByText(/no revisions uploaded yet/i);
    const fileInput = document.querySelector('input[type="file"]:not([accept^="image/"])') as HTMLInputElement;
    const file = new File(['x'], 'build.zip', { type: 'application/zip' });
    await userEvent.upload(fileInput, file);

    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalled();
    });
    expect(vi.mocked(toast.error).mock.calls[0][0]).toMatch(/too big/i);
  });

  it('has a Cancel button that does not call updateProduct', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    await screen.findByText('Acme Pro');

    await userEvent.click(screen.getByRole('button', { name: /^cancel$/i }));
    expect(vi.mocked(updateProduct)).not.toHaveBeenCalled();
  });

  it('renders an Edit product page link to the rich page editor', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    await screen.findByText('Acme Pro');
    const link = await screen.findByRole('link', { name: /edit product page/i });
    expect(link.getAttribute('href')).toBe('/admin/products/p1/page');
  });

  it('lists licences for the product and links each row to the licence detail page', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    vi.mocked(fetchLicences).mockResolvedValue({
      items: [
        {
          id: 'lic-1',
          productId: 'p1',
          productSlug: 'acme-pro',
          userId: 'u1',
          userEmail: 'alice@example.com',
          status: 'active',
          expiresAt: null,
          notes: null,
          hwidBound: true,
          hasKey: true,
          ipAllowlist: null,
          label: null,
          createdAt: '2026-04-01T00:00:00Z',
        },
      ],
      total: 1,
      limit: 25,
      offset: 0,
    });
    renderDetail();
    await screen.findByText('Acme Pro');

    await screen.findByText('alice@example.com');
    const viewLink = screen.getByRole('link', { name: /^view$/i });
    expect(vi.mocked(fetchLicences)).toHaveBeenCalledWith({ productId: 'p1', limit: 25, offset: 0 });
    expect(viewLink.getAttribute('href')).toBe('/admin/licences/lic-1');
  });

  it('shows an empty-state message when the product has no licences', async () => {
    vi.mocked(fetchProduct).mockResolvedValue(makeProduct());
    renderDetail();
    await screen.findByText('Acme Pro');
    expect(await screen.findByText(/no licences for this product/i)).toBeInTheDocument();
  });

});
