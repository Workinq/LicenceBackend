import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { Header } from '../components/layout/Header';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderHeader() {
  const rootRoute = createRootRoute({ component: () => <Header /> });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => null });
  const meRoute = createRoute({ getParentRoute: () => rootRoute, path: '/me', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, meRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  render(<RouterProvider router={router} />);
}

beforeEach(() => {
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id: 'u1',
    email: 'admin@example.com',
    displayName: 'Admin',
    role: 'admin',
    status: 'active',
    createdAt: new Date().toISOString(),
  });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
  vi.restoreAllMocks();
});

describe('Header', () => {
  it('shows the app title', async () => {
    renderHeader();
    expect(await screen.findByText('LicenceBackend')).toBeInTheDocument();
  });

  it('opens the user menu and shows the email, profile link, and sign-out actions', async () => {
    renderHeader();
    await userEvent.click(await screen.findByRole('button', { name: /account menu/i }));
    expect((await screen.findAllByText('admin@example.com')).length).toBeGreaterThan(0);
    expect(screen.getByRole('menuitem', { name: /my profile/i })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /^sign out$/i })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /sign out everywhere/i })).toBeInTheDocument();
  });
});
