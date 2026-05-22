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

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { toast } from 'sonner';
import { Route as LoginRoute } from '../routes/login';
import { useAccessTokenStore } from '../auth/access-token-store';

function renderLogin(initial = '/login') {
  const rootRoute = createRootRoute();
  const loginRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/login',
    component: LoginRoute.options.component,
    validateSearch: LoginRoute.options.validateSearch,
  });
  const adminRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin', component: () => <div>admin home</div> });
  const portalRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal', component: () => <div>portal home</div> });
  const forgotRoute = createRoute({ getParentRoute: () => rootRoute, path: '/forgot-password', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([loginRoute, adminRoute, portalRoute, forgotRoute]),
    history: createMemoryHistory({ initialEntries: [initial] }),
  });
  render(<RouterProvider router={router} />);
}

beforeEach(() => {
  vi.mocked(toast.error).mockReset();
  useAccessTokenStore.getState().clear();
});

afterEach(() => {
  vi.restoreAllMocks();
  useAccessTokenStore.getState().clear();
});

describe('LoginPage', () => {
  it('renders the sign in heading and email/password fields', async () => {
    renderLogin();
    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
  });

  it('toggles password visibility when the eye button is clicked', async () => {
    renderLogin();
    const password = await screen.findByLabelText('Password');
    expect(password).toHaveAttribute('type', 'password');
    await userEvent.click(screen.getByRole('button', { name: /show password/i }));
    expect(password).toHaveAttribute('type', 'text');
    await userEvent.click(screen.getByRole('button', { name: /hide password/i }));
    expect(password).toHaveAttribute('type', 'password');
  });

  it('shows a validation error for invalid email', async () => {
    renderLogin();
    await screen.findByRole('heading', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'not-an-email');
    await userEvent.type(screen.getByLabelText('Password'), 'pw');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByText(/valid email/i)).toBeInTheDocument();
  });

  it('toasts admin_required reason on mount when search param is set', async () => {
    renderLogin('/login?reason=admin_required');
    await screen.findByRole('heading', { name: 'Sign in' });
    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith(expect.stringMatching(/admin only/i));
    });
  });

  it('stores session and navigates admins to /admin on a successful login', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        accessToken: 'tok-admin',
        accessTokenExpiresAt: new Date(Date.now() + 900_000).toISOString(),
        user: { id: 'u1', email: 'admin@example.com', displayName: null, role: 'admin', status: 'active', createdAt: new Date().toISOString() },
      }),
    } as Response);

    renderLogin();
    await screen.findByRole('heading', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'admin@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'secret123');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => {
      expect(useAccessTokenStore.getState().accessToken).toBe('tok-admin');
    });
    expect(fetchMock).toHaveBeenCalledWith('/api/sessions', expect.objectContaining({ method: 'POST', credentials: 'include' }));
    expect(await screen.findByText(/admin home/i)).toBeInTheDocument();
  });

  it('toasts invalid credentials when the API returns 401', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue({ ok: false, status: 401, json: async () => ({}) } as Response);
    renderLogin();
    await screen.findByRole('heading', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'wrong');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith(expect.stringMatching(/invalid email or password/i));
    });
    expect(useAccessTokenStore.getState().accessToken).toBeNull();
  });

  it('toasts a generic error when the API returns a non-ok non-401 status', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue({ ok: false, status: 500, json: async () => ({}) } as Response);
    renderLogin();
    await screen.findByRole('heading', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'whatever');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith(expect.stringMatching(/login failed/i));
    });
  });

  it('toasts a network error when fetch rejects', async () => {
    vi.spyOn(global, 'fetch').mockRejectedValue(new Error('boom'));
    renderLogin();
    await screen.findByRole('heading', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'user@example.com');
    await userEvent.type(screen.getByLabelText('Password'), 'whatever');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith(expect.stringMatching(/network error/i));
    });
  });
});
