import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

vi.mock('../api/users', () => ({ createUser: vi.fn() }));
import { createUser } from '../api/users';
import { QuickCreateUserDialog } from '../components/QuickCreateUserDialog';
import { ApiError } from '../auth/api-client';

function renderDialog(onCreated = vi.fn(), onOpenChange = vi.fn()) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <QuickCreateUserDialog open onOpenChange={onOpenChange} onCreated={onCreated} />
    </QueryClientProvider>,
  );
  return { onCreated, onOpenChange };
}

beforeEach(() => {
  vi.mocked(createUser).mockReset();
});

describe('QuickCreateUserDialog', () => {
  it('creates a user, reveals the generated password, and calls onCreated on Done', async () => {
    vi.mocked(createUser).mockResolvedValue({
      id: 'u-new', email: 'bob@example.com', displayName: null, role: 'user',
      status: 'active', createdAt: '2026-01-01T00:00:00Z',
    });
    const { onCreated, onOpenChange } = renderDialog();

    await userEvent.type(screen.getByLabelText(/email/i), 'bob@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByText(/user created/i)).toBeInTheDocument();
    expect(screen.getByText(/initial password/i)).toBeInTheDocument();

    const call = vi.mocked(createUser).mock.calls[0][0];
    expect(call.email).toBe('bob@example.com');
    expect(call.role).toBe('user');
    expect(call.password).toHaveLength(24);

    await userEvent.click(screen.getByRole('button', { name: /done/i }));
    expect(onCreated).toHaveBeenCalledWith(expect.objectContaining({ id: 'u-new' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('shows the API detail message when the create fails', async () => {
    vi.mocked(createUser).mockRejectedValue(new ApiError(409, { detail: 'email already in use' }));
    renderDialog();

    await userEvent.type(screen.getByLabelText(/email/i), 'bob@example.com');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));

    expect(await screen.findByText(/email already in use/i)).toBeInTheDocument();
  });

  it('blocks submit when the email is not valid', async () => {
    renderDialog();
    await userEvent.type(screen.getByLabelText(/email/i), 'not-an-email');
    await userEvent.click(screen.getByRole('button', { name: /create user/i }));
    expect(await screen.findByText(/valid email/i)).toBeInTheDocument();
    expect(vi.mocked(createUser)).not.toHaveBeenCalled();
  });
});
