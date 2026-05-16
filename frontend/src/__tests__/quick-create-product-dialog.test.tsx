import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

vi.mock('../api/products', () => ({ createProduct: vi.fn() }));
import { createProduct } from '../api/products';
import { QuickCreateProductDialog } from '../components/QuickCreateProductDialog';
import { ApiError } from '../auth/api-client';

function renderDialog(onCreated = vi.fn(), onOpenChange = vi.fn()) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <QuickCreateProductDialog open onOpenChange={onOpenChange} onCreated={onCreated} />
    </QueryClientProvider>,
  );
  return { onCreated, onOpenChange };
}

beforeEach(() => {
  vi.mocked(createProduct).mockReset();
});

describe('QuickCreateProductDialog', () => {
  it('calls onCreated with the new product on a happy submit', async () => {
    vi.mocked(createProduct).mockResolvedValue({
      id: 'p-new', slug: 'acme', displayName: 'Acme', description: null, tagline: null,
      isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null,
      createdAt: '2026-01-01T00:00:00Z',
    });
    const { onCreated } = renderDialog();

    await userEvent.type(screen.getByLabelText(/slug/i), 'acme');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));

    expect(vi.mocked(createProduct)).toHaveBeenCalledWith(expect.objectContaining({ slug: 'acme', displayName: 'Acme' }));
    expect(onCreated).toHaveBeenCalledWith(expect.objectContaining({ id: 'p-new' }));
  });

  it('shows the API detail message when the create fails', async () => {
    vi.mocked(createProduct).mockRejectedValue(new ApiError(409, { detail: 'slug already taken' }));
    renderDialog();

    await userEvent.type(screen.getByLabelText(/slug/i), 'acme');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Acme');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));

    expect(await screen.findByText(/slug already taken/i)).toBeInTheDocument();
  });

  it('blocks submit with a slug regex error', async () => {
    renderDialog();
    await userEvent.type(screen.getByLabelText(/slug/i), 'NOT VALID');
    await userEvent.type(screen.getByLabelText(/display name/i), 'X');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));
    expect(await screen.findByText(/lowercase letters/i)).toBeInTheDocument();
    expect(vi.mocked(createProduct)).not.toHaveBeenCalled();
  });
});
