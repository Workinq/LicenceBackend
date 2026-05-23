import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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
  fetchUser: vi.fn(),
  fetchUserLicences: vi.fn(),
  updateUserStatus: vi.fn(),
  fetchUsers: vi.fn(),
  createUser: vi.fn(),
}));
vi.mock('../api/audit-events', () => ({ fetchAuditEvents: vi.fn() }));

import { fetchUser, fetchUserLicences, updateUserStatus } from '../api/users';
import { fetchAuditEvents } from '../api/audit-events';
import { ApiError } from '../auth/api-client';
import { useAccessTokenStore } from '../auth/access-token-store';
import { Route as UserDetailRoute } from '../routes/admin/users_.$id';

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

function licence(over: Record<string, unknown> = {}) {
  return {
    id: 'lic-1',
    productId: 'p-1',
    productSlug: 'acme-pro',
    userId: 'u-1',
    userEmail: 'alice@example.com',
    status: 'active',
    expiresAt: null,
    notes: null,
    hwidBound: false,
    hasKey: true,
    ipAllowlist: null,
    label: null,
    createdAt: '2026-01-02T00:00:00Z',
    relationship: 'owner',
    ...over,
  };
}

function renderDetail(id = 'u-1') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users/$id',
    component: UserDetailRoute.options.component,
  });
  const licenceDetailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/licences/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute, licenceDetailRoute]),
    history: createMemoryHistory({ initialEntries: [`/admin/users/${id}`] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchUser).mockReset();
  vi.mocked(fetchUserLicences).mockReset();
  vi.mocked(updateUserStatus).mockReset();
  vi.mocked(fetchAuditEvents).mockReset();
  vi.mocked(fetchUserLicences).mockResolvedValue({ items: [], total: 0, limit: 10, offset: 0 });
  vi.mocked(fetchAuditEvents).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('AdminUserDetailRoute', () => {
  it('shows a failure message when the user query errors', async () => {
    vi.mocked(fetchUser).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load this user/i)).toBeInTheDocument();
  });

  it('renders the profile card with email, role, and status once loaded', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user({ displayName: 'Alice', role: 'admin' }));
    renderDetail();
    expect(await screen.findByRole('heading', { name: 'alice@example.com' })).toBeInTheDocument();
    expect(screen.getByText('Profile')).toBeInTheDocument();
    expect(screen.getAllByText('Alice').length).toBeGreaterThan(0);
    expect(screen.getAllByText('admin').length).toBeGreaterThan(0);
  });

  it('shows the empty licences message when the user has no licences', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user());
    vi.mocked(fetchUserLicences).mockResolvedValue({ items: [], total: 0, limit: 10, offset: 0 });
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });
    expect(await screen.findByText(/this user has no licences/i)).toBeInTheDocument();
  });

  it('lists the user licences with product slug and status', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user());
    vi.mocked(fetchUserLicences).mockResolvedValue({
      items: [licence(), licence({ id: 'lic-2', productSlug: 'acme-lite', status: 'revoked', relationship: 'member' })],
      total: 2,
      limit: 10,
      offset: 0,
    });
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });
    expect(await screen.findByRole('link', { name: /view licence acme-pro/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view licence acme-lite/i })).toBeInTheDocument();
    expect(screen.getByText('revoked')).toBeInTheDocument();
    expect(screen.getByText('member')).toBeInTheDocument();
  });

  it('shows a Suspend trigger for an active other user and calls updateUserStatus when confirmed', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user({ status: 'active' }));
    vi.mocked(updateUserStatus).mockResolvedValue(user({ status: 'suspended' }));
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });

    await userEvent.click(screen.getByRole('button', { name: /^suspend$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.type(within(dialog).getByLabelText(/reason/i), 'Spam');
    await userEvent.click(within(dialog).getByRole('button', { name: /suspend user/i }));

    expect(vi.mocked(updateUserStatus)).toHaveBeenCalledWith('u-1', { status: 'suspended', reason: 'Spam' });
  });

  it('disables the Suspend button when viewing your own account', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u-1',
      email: 'alice@example.com',
      displayName: null,
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    vi.mocked(fetchUser).mockResolvedValue(user({ status: 'active' }));
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });
    expect(screen.getByRole('button', { name: /^suspend$/i })).toBeDisabled();
  });

  it('shows a Reactivate button for a suspended user and calls updateUserStatus with active', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user({ status: 'suspended' }));
    vi.mocked(updateUserStatus).mockResolvedValue(user({ status: 'active' }));
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });

    await userEvent.click(screen.getByRole('button', { name: /reactivate/i }));
    expect(vi.mocked(updateUserStatus)).toHaveBeenCalledWith('u-1', { status: 'active', reason: null });
  });

  it('shows the API error detail when updateUserStatus fails', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user({ status: 'suspended' }));
    vi.mocked(updateUserStatus).mockRejectedValue(new ApiError(400, { detail: 'Cannot reactivate.' }));
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });

    await userEvent.click(screen.getByRole('button', { name: /reactivate/i }));
    expect(await screen.findByText(/cannot reactivate/i)).toBeInTheDocument();
  });

  it('renders the audit history empty state when there are no events', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user());
    vi.mocked(fetchAuditEvents).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });
    expect(await screen.findByText(/no activity yet/i)).toBeInTheDocument();
  });

  it('renders audit history rows describing status changes', async () => {
    vi.mocked(fetchUser).mockResolvedValue(user());
    vi.mocked(fetchAuditEvents).mockResolvedValue({
      items: [
        {
          id: 'evt-1',
          occurredAt: '2026-02-01T00:00:00Z',
          eventType: 'user.status_changed',
          subjectType: 'user',
          subjectId: 'u-1',
          actorType: 'admin',
          actorUserId: 'a-1',
          actorUserEmail: 'admin@example.com',
          reason: 'Spam',
          payload: { previousStatus: 'active', newStatus: 'suspended' } as unknown as never,
        },
      ],
      total: 1,
      limit: 20,
      offset: 0,
    });
    renderDetail();
    await screen.findByRole('heading', { name: 'alice@example.com' });
    expect(await screen.findByText(/active -> suspended/i)).toBeInTheDocument();
  });
});
