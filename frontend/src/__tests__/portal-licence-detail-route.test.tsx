import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  createRootRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';

vi.mock('../api/me-licences', () => ({
  fetchMyLicence: vi.fn(),
  fetchMyLicenceMembers: vi.fn(),
  fetchMyLicenceSeats: vi.fn(),
  addMyLicenceMember: vi.fn(),
  removeMyLicenceMember: vi.fn(),
  regenerateMyLicenceKey: vi.fn(),
  updateMyLicenceLabel: vi.fn(),
  downloadMyLicenceFile: vi.fn(),
}));
vi.mock('../api/product-files', () => ({
  triggerBlobDownload: vi.fn(),
}));
vi.mock('../api/checkouts', () => ({
  checkinSeat: vi.fn(),
}));
vi.mock('sonner', () => ({
  toast: { info: vi.fn(), error: vi.fn(), success: vi.fn() },
}));

import {
  fetchMyLicence,
  fetchMyLicenceMembers,
  fetchMyLicenceSeats,
} from '../api/me-licences';
import { Route as LicenceDetailRoute } from '../routes/portal/licences_.$id';
import type { LicenceResponse, LicenceMemberResponse } from '../api/generated/api.schemas';

function licence(over: Partial<LicenceResponse> = {}): LicenceResponse {
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
    createdAt: '2026-01-01T00:00:00Z',
    orderId: null,
    relationship: 'owner',
    ...over,
  };
}

function renderDetail() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const detailRoute = LicenceDetailRoute.update({
    id: '/portal/licences_/$id',
    path: '/portal/licences/$id',
    getParentRoute: () => rootRoute,
  } as never);
  const router = createRouter({
    routeTree: rootRoute.addChildren([detailRoute as never]),
    history: createMemoryHistory({ initialEntries: ['/portal/licences/lic-1'] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMyLicence).mockReset();
  vi.mocked(fetchMyLicenceMembers).mockReset();
  vi.mocked(fetchMyLicenceSeats).mockReset();
  vi.mocked(fetchMyLicenceSeats).mockResolvedValue({
    maxSeats: 5,
    live: [],
    history: { items: [], total: 0, limit: 20, offset: 0 },
  });
});

describe('PortalLicenceDetailRoute', () => {
  it('shows the loading skeleton while the licence query is pending', async () => {
    vi.mocked(fetchMyLicence).mockReturnValue(new Promise(() => {}));
    const { container } = renderDetail();
    await waitFor(() => {
      expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
    });
  });

  it('shows a failure message when the licence query fails', async () => {
    vi.mocked(fetchMyLicence).mockRejectedValue(new Error('boom'));
    renderDetail();
    expect(await screen.findByText(/failed to load this licence/i)).toBeInTheDocument();
  });

  it('renders the product slug, owner email, and active status when loaded', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence());
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue([]);
    renderDetail();
    expect(await screen.findByRole('heading', { name: /acme-pro/i })).toBeInTheDocument();
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getAllByText(/active/i).length).toBeGreaterThan(0);
  });

  it('shows the regenerate-key copy when the licence already has a key', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence({ hasKey: true }));
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue([]);
    renderDetail();
    await screen.findByRole('heading', { name: /acme-pro/i });
    expect(
      screen.getByText(/the licence key is shown only once at creation/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /regenerate key/i })).toBeInTheDocument();
  });

  it('shows the generate-key copy when the licence has no key yet', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence({ hasKey: false }));
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue([]);
    renderDetail();
    await screen.findByRole('heading', { name: /acme-pro/i });
    expect(
      screen.getByText(/this licence has no key yet/i),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^generate key$/i })).toBeInTheDocument();
  });

  it('renders the Members section with empty state for the owner', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence());
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue([]);
    renderDetail();
    expect(await screen.findByText(/no members yet/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/add member by email/i)).toBeInTheDocument();
  });

  it('renders members in the list when the members query returns rows', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence());
    const members: LicenceMemberResponse[] = [
      { userId: 'm-1', email: 'bob@example.com', addedAt: '2026-04-01T00:00:00Z' } as LicenceMemberResponse,
    ];
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue(members);
    renderDetail();
    await waitFor(() => {
      expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    });
    expect(screen.getByRole('button', { name: /remove bob@example.com/i })).toBeInTheDocument();
  });

  it('hides the Members section for member relationships', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence({ relationship: 'member' }));
    renderDetail();
    await screen.findByRole('heading', { name: /acme-pro/i });
    expect(screen.queryByLabelText(/add member by email/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^generate key$|^regenerate key$/i })).toBeDisabled();
  });

  it('disables the Add member submit button until an email is typed', async () => {
    vi.mocked(fetchMyLicence).mockResolvedValue(licence());
    vi.mocked(fetchMyLicenceMembers).mockResolvedValue([]);
    renderDetail();
    const addBtn = await screen.findByRole('button', { name: /^add$/i });
    expect(addBtn).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/add member by email/i), 'new@example.com');
    expect(addBtn).not.toBeDisabled();
  });
});
