import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
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
  createUser: vi.fn(),
  fetchUsers: vi.fn(),
  fetchUser: vi.fn(),
  updateUserStatus: vi.fn(),
  fetchUserLicences: vi.fn(),
}));
import { createUser } from '../api/users';
import { ApiError } from '../auth/api-client';
import { Route as NewUserRoute } from '../routes/admin/users_.new';

function renderNew() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const rootRoute = createRootRoute();
  const newRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users/new',
    component: NewUserRoute.options.component,
  });
  const usersRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users',
    component: () => null,
  });
  const detailRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/admin/users/$id',
    component: () => null,
  });
  const router = createRouter({
    routeTree: rootRoute.addChildren([newRoute, usersRoute, detailRoute]),
    history: createMemoryHistory({ initialEntries: ['/admin/users/new'] }),
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

const swallow = () => {};

beforeEach(() => {
  vi.mocked(createUser).mockReset();
  process.on('unhandledRejection', swallow);
  if (typeof window !== 'undefined') {
    window.addEventListener('unhandledrejection', swallow);
  }
});

afterEach(() => {
  process.off('unhandledRejection', swallow);
  if (typeof window !== 'undefined') {
    window.removeEventListener('unhandledrejection', swallow);
  }
});

describe('AdminUserNewRoute', () => {
  it('renders the create form with email and display name fields and submit/cancel buttons', async () => {
    renderNew();
    expect(await screen.findByRole('heading', { name: /new user/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/^email$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create user/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^cancel$/i })).toBeInTheDocument();
  });

  it('shows a required validation error when email is empty on submit', async () => {
    renderNew();
    await userEvent.click(await screen.findByRole('button', { name: /create user/i }));
    expect(await screen.findByText(/required/i)).toBeInTheDocument();
    expect(vi.mocked(createUser)).not.toHaveBeenCalled();
  });

  it('shows an invalid-email error when the email is not well-formed', async () => {
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'not-an-email');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));
    expect(await screen.findByText(/enter a valid email/i)).toBeInTheDocument();
    expect(vi.mocked(createUser)).not.toHaveBeenCalled();
  });

  it('submits the email and a generated password and shows the reveal-once panel on success', async () => {
    vi.mocked(createUser).mockResolvedValue({
      id: 'u-new',
      email: 'newbie@example.com',
      displayName: null,
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'newbie@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByRole('heading', { name: /user created/i })).toBeInTheDocument();
    expect(screen.getByText(/newbie@example\.com/)).toBeInTheDocument();
    expect(vi.mocked(createUser)).toHaveBeenCalledTimes(1);
    const args = vi.mocked(createUser).mock.calls[0][0];
    expect(args.email).toBe('newbie@example.com');
    expect(typeof args.password).toBe('string');
    expect((args.password as string).length).toBeGreaterThanOrEqual(16);
    expect(args.displayName).toBeNull();
  });

  it('sends displayName when one is entered', async () => {
    vi.mocked(createUser).mockResolvedValue({
      id: 'u-new',
      email: 'newbie@example.com',
      displayName: 'Newbie',
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'newbie@example.com');
    await userEvent.type(screen.getByLabelText(/display name/i), 'Newbie');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));

    await screen.findByRole('heading', { name: /user created/i });
    expect(vi.mocked(createUser)).toHaveBeenCalledWith(
      expect.objectContaining({ email: 'newbie@example.com', displayName: 'Newbie' }),
    );
  });

  it('renders an Open user and Back to users button after creation', async () => {
    vi.mocked(createUser).mockResolvedValue({
      id: 'u-new',
      email: 'newbie@example.com',
      displayName: null,
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'newbie@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByRole('button', { name: /open user/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /back to users/i })).toBeInTheDocument();
  });

  it('shows the API error detail when the server rejects the request', async () => {
    vi.mocked(createUser).mockRejectedValue(new ApiError(409, { detail: 'Email already exists.' }));
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'newbie@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));
    expect(await screen.findByText(/email already exists/i)).toBeInTheDocument();
  });

  it('shows the generic error message when the failure is not an ApiError', async () => {
    vi.mocked(createUser).mockRejectedValue(new Error('boom'));
    renderNew();
    await userEvent.type(await screen.findByLabelText(/^email$/i), 'newbie@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));
    expect(await screen.findByText(/could not create the user/i)).toBeInTheDocument();
  });
});
