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

vi.mock('../api/me', () => ({
  fetchMe: vi.fn(),
  updateProfile: vi.fn(),
  changePassword: vi.fn(),
}));

import { fetchMe } from '../api/me';
import { Route as AdminMeRoute } from '../routes/admin/me';

function renderAdminMe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const meRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/me',
    component: AdminMeRoute.options.component,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([meRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/me'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMe).mockReset();
});

describe('AdminMeRoute', () => {
  it('renders the profile editor headings once the profile loads', async () => {
    vi.mocked(fetchMe).mockResolvedValue({
      id: 'u1',
      email: 'admin@example.com',
      displayName: 'Admin',
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderAdminMe();
    expect(await screen.findByRole('heading', { name: /my profile/i })).toBeInTheDocument();
    expect(screen.getByText('admin@example.com')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^change password$/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
  });
});
