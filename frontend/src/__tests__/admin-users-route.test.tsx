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

vi.mock('../api/users', () => ({
  fetchUsers: vi.fn(),
  fetchUser: vi.fn(),
  createUser: vi.fn(),
  updateUserStatus: vi.fn(),
  fetchUserLicences: vi.fn(),
}));
import { fetchUsers } from '../api/users';
import { Route as UsersRoute } from '../routes/admin/users';

function user(over: Record<string, unknown> = {}) {
  return {
    id: 'u-1',
    email: 'alice@example.com',
    displayName: null,
    role: 'user',
    status: 'active',
    createdAt: '2026-01-01T00:00:00Z',
    ...over,
  };
}

function renderUsers() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const usersRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users',
    component: UsersRoute.options.component,
  });
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users/$id',
    component: () => null,
  });
  const newRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users/new',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([usersRoute, detailRoute, newRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/users'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchUsers).mockReset();
});

describe('AdminUsersRoute', () => {
  it('renders a row per user with email, role, and status', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({
      items: [
        user(),
        user({ id: 'u-2', email: 'bob@example.com', role: 'admin', status: 'suspended' }),
      ],
      total: 2,
      limit: 25,
      offset: 0,
    });
    renderUsers();
    expect(await screen.findByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    expect(screen.getByText('admin')).toBeInTheDocument();
    expect(screen.getByText('suspended')).toBeInTheDocument();
  });

  it('has a New user link pointing at /admin/users/new', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderUsers();
    const link = await screen.findByRole('link', { name: /new user/i });
    expect(link).toHaveAttribute('href', '/admin/users/new');
  });

  it('shows an empty-state message when no users match the filters', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderUsers();
    expect(await screen.findByText(/no users match these filters/i)).toBeInTheDocument();
  });

  it('shows an error message when the query fails', async () => {
    vi.mocked(fetchUsers).mockRejectedValue(new Error('boom'));
    renderUsers();
    expect(await screen.findByText(/failed to load users/i)).toBeInTheDocument();
  });

  it('passes the trimmed search box value to fetchUsers as q and resets offset', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({
      items: [user()],
      total: 1,
      limit: 25,
      offset: 0,
    });
    renderUsers();
    await screen.findByText('alice@example.com');
    await userEvent.type(screen.getByPlaceholderText(/search by email/i), 'alice');
    const calls = vi.mocked(fetchUsers).mock.calls;
    expect(calls.at(-1)?.[0]).toEqual(expect.objectContaining({ q: 'alice', offset: 0, limit: 25 }));
  });

  it('omits the q param when search is empty', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({ items: [], total: 0, limit: 25, offset: 0 });
    renderUsers();
    await screen.findByText(/no users match/i);
    const firstCall = vi.mocked(fetchUsers).mock.calls[0]?.[0];
    expect(firstCall).toEqual({ limit: 25, offset: 0 });
  });

  it('disables Previous on the first page and disables Next when there is no next page', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({
      items: [user()],
      total: 1,
      limit: 25,
      offset: 0,
    });
    renderUsers();
    await screen.findByText('alice@example.com');
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /next/i })).toBeDisabled();
  });

  it('shows the range label as 1-N of total when data has loaded', async () => {
    vi.mocked(fetchUsers).mockResolvedValue({
      items: [user()],
      total: 1,
      limit: 25,
      offset: 0,
    });
    renderUsers();
    expect(await screen.findByText('1-1 of 1')).toBeInTheDocument();
  });
});
