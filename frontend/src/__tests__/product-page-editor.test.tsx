import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProductPageEditor } from '../components/products/ProductPageEditor';
import { ApiError } from '../auth/api-client';

vi.mock('../api/products', () => ({
  updateProduct: vi.fn(),
}));
vi.mock('../api/product-content-images', () => ({
  uploadProductContentImage: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { updateProduct } from '../api/products';
import { uploadProductContentImage } from '../api/product-content-images';
import { toast } from 'sonner';

function renderEditor(props: {
  initialContent?: Record<string, unknown> | null;
  onDirtyChange?: (dirty: boolean) => void;
} = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <ProductPageEditor
        productId="prod-1"
        initialContent={props.initialContent ?? null}
        onDirtyChange={props.onDirtyChange}
      />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(updateProduct).mockReset();
  vi.mocked(uploadProductContentImage).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
});

describe('ProductPageEditor', () => {
  it('renders the toolbar and the Save button', async () => {
    renderEditor();
    expect(await screen.findByRole('button', { name: /save product page/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Bold' })).toBeInTheDocument();
  });

  it('clicking Save calls updateProduct with the current editor JSON', async () => {
    vi.mocked(updateProduct).mockResolvedValue({ id: 'prod-1' } as never);
    renderEditor({ initialContent: { type: 'doc', content: [{ type: 'paragraph' }] } });

    await userEvent.click(await screen.findByRole('button', { name: /save product page/i }));

    await waitFor(() => {
      expect(vi.mocked(updateProduct)).toHaveBeenCalledTimes(1);
    });
    const [productId, body] = vi.mocked(updateProduct).mock.calls[0];
    expect(productId).toBe('prod-1');
    expect(body.displayName).toBeNull();
    expect(body.pageContent).toEqual(expect.objectContaining({ type: 'doc' }));
  });

  it('toasts success after a successful save', async () => {
    vi.mocked(updateProduct).mockResolvedValue({ id: 'prod-1' } as never);
    renderEditor();

    await userEvent.click(await screen.findByRole('button', { name: /save product page/i }));

    await waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith('Product page saved.');
    });
  });

  it('toasts the API detail message when save fails', async () => {
    vi.mocked(updateProduct).mockRejectedValue(
      new ApiError(400, { detail: 'Bad doc.' }),
    );
    renderEditor();

    await userEvent.click(await screen.findByRole('button', { name: /save product page/i }));

    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Bad doc.');
    });
  });

  it('falls back to a generic save error message when no detail is present', async () => {
    vi.mocked(updateProduct).mockRejectedValue(new Error('net'));
    renderEditor();

    await userEvent.click(await screen.findByRole('button', { name: /save product page/i }));

    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Could not save the product page.');
    });
  });

  it('uploads a selected image file and clears the input', async () => {
    vi.mocked(uploadProductContentImage).mockResolvedValue({ url: '/uploads/img.png' } as never);
    const { container } = renderEditor();

    await screen.findByRole('button', { name: /save product page/i });
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    expect(input).not.toBeNull();
    const file = new File(['x'], 'pic.png', { type: 'image/png' });
    await userEvent.upload(input, file);

    await waitFor(() => {
      expect(vi.mocked(uploadProductContentImage)).toHaveBeenCalledWith('prod-1', file);
    });
    expect(input.value).toBe('');
  });

  it('toasts an error when the image upload fails', async () => {
    vi.mocked(uploadProductContentImage).mockRejectedValue(
      new ApiError(413, { detail: 'Too big.' }),
    );
    const { container } = renderEditor();
    await screen.findByRole('button', { name: /save product page/i });
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['x'], 'pic.png', { type: 'image/png' });
    await userEvent.upload(input, file);

    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Too big.');
    });
  });

  it('reports dirty when an action mutates the document', async () => {
    const onDirtyChange = vi.fn();
    vi.mocked(uploadProductContentImage).mockResolvedValue({ url: '/uploads/img.png' } as never);
    const { container } = renderEditor({ onDirtyChange });
    await screen.findByRole('button', { name: /save product page/i });

    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['x'], 'pic.png', { type: 'image/png' });
    await userEvent.upload(input, file);

    await waitFor(() => {
      expect(onDirtyChange).toHaveBeenCalledWith(true);
    });
  });

  it('reports not-dirty after a successful save', async () => {
    const onDirtyChange = vi.fn();
    vi.mocked(updateProduct).mockResolvedValue({ id: 'prod-1' } as never);
    vi.mocked(uploadProductContentImage).mockResolvedValue({ url: '/uploads/img.png' } as never);
    const { container } = renderEditor({ onDirtyChange });
    await screen.findByRole('button', { name: /save product page/i });

    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(fileInput, new File(['x'], 'pic.png', { type: 'image/png' }));
    await waitFor(() => {
      expect(onDirtyChange).toHaveBeenCalledWith(true);
    });

    await userEvent.click(screen.getByRole('button', { name: /save product page/i }));
    await waitFor(() => {
      expect(onDirtyChange).toHaveBeenCalledWith(false);
    });
  });
});
