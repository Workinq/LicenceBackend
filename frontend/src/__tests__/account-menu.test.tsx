import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { AccountMenu } from '../components/layout/AccountMenu';
import { useAccessTokenStore } from '../auth/access-token-store';

vi.mock('../auth/api-client', () => ({
  apiClient: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    body: unknown;
    constructor(status: number, body: unknown) {
      super(`API error ${status}`);
      this.status = status;
      this.body = body;
    }
  },
}));
import { apiClient } from '../auth/api-client';

const originalLocation = window.location;
const assignMock = vi.fn();

function renderMenu() {
  const rootRoute = createRootRoute({ component: () => <AccountMenu profileHref="/admin/me" /> });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => null });
  const profileRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/me', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, profileRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  render(<RouterProvider router={router} />);
}

beforeEach(() => {
  Object.defineProperty(window, 'location', {
    value: { assign: assignMock },
    writable: true,
    configurable: true,
  });
  assignMock.mockReset();
  vi.mocked(apiClient).mockReset();
  vi.mocked(apiClient).mockResolvedValue(undefined as never);
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
  Object.defineProperty(window, 'location', {
    value: originalLocation,
    writable: true,
    configurable: true,
  });
  useAccessTokenStore.getState().clear();
  vi.restoreAllMocks();
});

describe('AccountMenu', () => {
  it('shows the signed-in user email on the trigger', async () => {
    renderMenu();
    expect(await screen.findByRole('button', { name: /account menu/i })).toHaveTextContent('admin@example.com');
  });

  it('calls apiClient DELETE /sessions and redirects to /login on Sign out', async () => {
    renderMenu();
    await userEvent.click(await screen.findByRole('button', { name: /account menu/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: /^sign out$/i }));
    await waitFor(() => {
      expect(vi.mocked(apiClient)).toHaveBeenCalledWith('/sessions', { method: 'DELETE' });
    });
    await waitFor(() => {
      expect(assignMock).toHaveBeenCalledWith('/login');
    });
    expect(useAccessTokenStore.getState().accessToken).toBeNull();
  });

  it('calls apiClient DELETE /sessions/all and redirects to /login on Sign out everywhere', async () => {
    renderMenu();
    await userEvent.click(await screen.findByRole('button', { name: /account menu/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: /sign out everywhere/i }));
    await waitFor(() => {
      expect(vi.mocked(apiClient)).toHaveBeenCalledWith('/sessions/all', { method: 'DELETE' });
    });
    await waitFor(() => {
      expect(assignMock).toHaveBeenCalledWith('/login');
    });
    expect(useAccessTokenStore.getState().accessToken).toBeNull();
  });

  it('still clears the session and redirects when the sessions DELETE fails', async () => {
    const swallow = (): void => {};
    process.on('unhandledRejection', swallow);
    try {
      vi.mocked(apiClient).mockRejectedValueOnce(new Error('network'));
      renderMenu();
      await userEvent.click(await screen.findByRole('button', { name: /account menu/i }));
      await userEvent.click(await screen.findByRole('menuitem', { name: /^sign out$/i }));
      await waitFor(() => {
        expect(assignMock).toHaveBeenCalledWith('/login');
      });
      expect(useAccessTokenStore.getState().accessToken).toBeNull();
    } finally {
      process.off('unhandledRejection', swallow);
    }
  });
});
