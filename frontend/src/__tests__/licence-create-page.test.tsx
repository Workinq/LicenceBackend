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

vi.mock('../api/products', () => ({ fetchProducts: vi.fn(), createProduct: vi.fn() }));
vi.mock('../api/users', () => ({ fetchUsers: vi.fn(), createUser: vi.fn() }));
vi.mock('../api/licences', () => ({ createLicence: vi.fn(), fetchLicences: vi.fn(), fetchLicence: vi.fn() }));
import { fetchProducts, createProduct } from '../api/products';
import { fetchUsers, createUser } from '../api/users';
import { createLicence } from '../api/licences';
import { Route as NewLicenceRoute } from '../routes/admin/licences_.new';

function renderNew() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const newRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences/new', component: NewLicenceRoute.options.component });
  const detailRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences/$id', component: () => null });
  const listRoute = createRoute({ getParentRoute: () => rootRoute, path: '/licences', component: () => null });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([newRoute, detailRoute, listRoute, productsRoute]),
    history: createMemoryHistory({ initialEntries: ['/licences/new'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchProducts).mockReset();
  vi.mocked(fetchUsers).mockReset();
  vi.mocked(createLicence).mockReset();
  vi.mocked(createProduct).mockReset();
  vi.mocked(createUser).mockReset();
});

describe('NewLicencePage', () => {
  it('renders the create form with product and user fields once the lists load', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    renderNew();
    expect(await screen.findByRole('button', { name: /create licence/i })).toBeInTheDocument();
    expect(screen.getByText(/product/i)).toBeInTheDocument();
    expect(screen.getByText(/user/i)).toBeInTheDocument();
  });

  it('keeps the form usable and shows an empty state in the product picker when there are no products', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [], total: 0, limit: 200, offset: 0 });
    renderNew();
    expect(await screen.findByRole('button', { name: /create licence/i })).toBeInTheDocument();
    const productCombobox = screen.getAllByRole('combobox')[0];
    await userEvent.click(productCombobox);
    await userEvent.type(await screen.findByPlaceholderText('Search products'), 'x');
    expect(await screen.findByText(/there are no products/i)).toBeInTheDocument();
  });

  it('submits ipAllowlist as [] when Restrict by IP is toggled on with no CIDRs', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(createLicence).mockResolvedValue({
      id: 'lic-1', productId: 'p1', productSlug: 'acme-pro', userId: 'u1', userEmail: 'alice@example.com',
      status: 'active', expiresAt: null, notes: null, hwidBound: false, ipAllowlist: [], label: null, createdAt: '2026-01-01T00:00:00Z', licenceKey: 'KEY-1',
    });
    renderNew();
    await screen.findByRole('button', { name: /create licence/i });

    await userEvent.click(screen.getAllByRole('combobox')[0]);
    await userEvent.click(await screen.findByText('Acme Pro'));
    await userEvent.click(screen.getAllByRole('combobox')[1]);
    await userEvent.click(await screen.findByText('alice@example.com'));
    await userEvent.click(screen.getByRole('switch', { name: /restrict by ip/i }));
    await userEvent.click(screen.getByRole('button', { name: /create licence/i }));

    expect(vi.mocked(createLicence)).toHaveBeenCalledWith(expect.objectContaining({ ipAllowlist: [] }));
  });

  it('submits ipAllowlist with the entered CIDRs when Restrict by IP is on', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(createLicence).mockResolvedValue({
      id: 'lic-1', productId: 'p1', productSlug: 'acme-pro', userId: 'u1', userEmail: 'alice@example.com',
      status: 'active', expiresAt: null, notes: null, hwidBound: false, ipAllowlist: ['10.0.0.0/24'], label: null, createdAt: '2026-01-01T00:00:00Z', licenceKey: 'KEY-1',
    });
    renderNew();
    await screen.findByRole('button', { name: /create licence/i });

    await userEvent.click(screen.getAllByRole('combobox')[0]);
    await userEvent.click(await screen.findByText('Acme Pro'));
    await userEvent.click(screen.getAllByRole('combobox')[1]);
    await userEvent.click(await screen.findByText('alice@example.com'));
    await userEvent.click(screen.getByRole('switch', { name: /restrict by ip/i }));
    await userEvent.click(screen.getByRole('button', { name: /add cidr/i }));
    await userEvent.type(screen.getByPlaceholderText(/cidr/i), '10.0.0.0/24');
    await userEvent.click(screen.getByRole('button', { name: /create licence/i }));

    expect(vi.mocked(createLicence)).toHaveBeenCalledWith(expect.objectContaining({ ipAllowlist: ['10.0.0.0/24'] }));
  });

  it('shows a validation error when submitting without a product', async () => {
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    renderNew();
    const submitBtn = await screen.findByRole('button', { name: /create licence/i });
    await userEvent.click(submitBtn);
    expect(await screen.findByText(/choose a product/i)).toBeInTheDocument();
  });

  it('opens the quick-create product dialog from the product combobox footer and auto-selects the new product', async () => {
    const newProduct = {
      id: 'p-new', slug: 'fresh', displayName: 'Fresh Product', description: null, tagline: null,
      isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null,
      createdAt: '2026-01-01T00:00:00Z',
    };
    vi.mocked(fetchProducts)
      .mockResolvedValueOnce({ items: [], total: 0, limit: 200, offset: 0 })
      .mockResolvedValue({ items: [newProduct], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers).mockResolvedValue({ items: [{ id: 'u1', email: 'alice@example.com', displayName: null, role: 'admin', status: 'active', createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(createProduct).mockResolvedValue(newProduct);
    renderNew();
    await screen.findByRole('button', { name: /create licence/i });

    await userEvent.click(screen.getAllByRole('combobox')[0]);
    await userEvent.click(await screen.findByRole('button', { name: /create new product/i }));

    await userEvent.type(await screen.findByLabelText(/slug/i), 'fresh');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Fresh Product');
    await userEvent.click(screen.getByRole('button', { name: /create product/i }));

    expect(await screen.findByRole('combobox', { name: /fresh product/i })).toBeInTheDocument();
  });

  it('opens the quick-create user dialog and auto-selects the new user after Done', async () => {
    const newUser = {
      id: 'u-new', email: 'newbie@example.com', displayName: null, role: 'user',
      status: 'active', createdAt: '2026-01-01T00:00:00Z',
    };
    vi.mocked(fetchProducts).mockResolvedValue({ items: [{ id: 'p1', slug: 'acme-pro', displayName: 'Acme Pro', description: null, tagline: null, isPublic: true, price: null, currency: 'USD', sortOrder: 0, imageUrl: null, createdAt: '2026-01-01T00:00:00Z' }], total: 1, limit: 200, offset: 0 });
    vi.mocked(fetchUsers)
      .mockResolvedValueOnce({ items: [], total: 0, limit: 200, offset: 0 })
      .mockResolvedValue({ items: [newUser], total: 1, limit: 200, offset: 0 });
    vi.mocked(createUser).mockResolvedValue(newUser);
    renderNew();
    await screen.findByRole('button', { name: /create licence/i });

    await userEvent.click(screen.getAllByRole('combobox')[1]);
    await userEvent.click(await screen.findByRole('button', { name: /create new user/i }));

    await userEvent.type(await screen.findByLabelText(/email/i), 'newbie@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));
    await userEvent.click(await screen.findByRole('button', { name: /done/i }));

    expect(await screen.findByRole('combobox', { name: /newbie@example.com/i })).toBeInTheDocument();
  });
});
