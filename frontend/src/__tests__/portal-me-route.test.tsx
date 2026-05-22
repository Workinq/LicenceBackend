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
import { Route as PortalMeRoute } from '../routes/portal/me';

function renderPortalMe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const meRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/portal/me',
    component: PortalMeRoute.options.component,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([meRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/me'] }),
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

describe('PortalMeRoute', () => {
  it('renders the profile editor for the signed-in portal user', async () => {
    vi.mocked(fetchMe).mockResolvedValue({
      id: 'u2',
      email: 'user@example.com',
      displayName: null,
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderPortalMe();
    expect(await screen.findByRole('heading', { name: /my profile/i })).toBeInTheDocument();
    expect(screen.getByText('user@example.com')).toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
  });

  it('shows a failure message when the profile fails to load', async () => {
    vi.mocked(fetchMe).mockRejectedValue(new Error('boom'));
    renderPortalMe();
    expect(await screen.findByText(/failed to load your profile/i)).toBeInTheDocument();
  });
});
