import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProfileEditor } from '../components/ProfileEditor';
import { ApiError } from '../auth/api-client';
import { useAccessTokenStore } from '../auth/access-token-store';

vi.mock('../api/me', () => ({
  fetchMe: vi.fn(),
  updateProfile: vi.fn(),
  changePassword: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { fetchMe, updateProfile, changePassword } from '../api/me';
import { toast } from 'sonner';

const sampleUser = {
  id: 'u-1',
  email: 'me@example.com',
  displayName: 'Original Name',
  role: 'admin' as const,
  status: 'active' as const,
  createdAt: '2026-01-01T00:00:00Z',
};

function renderEditor() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <ProfileEditor />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchMe).mockReset();
  vi.mocked(updateProfile).mockReset();
  vi.mocked(changePassword).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
  useAccessTokenStore.getState().clear();
});

describe('ProfileEditor', () => {
  it('renders the account email, role and display name once loaded', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    renderEditor();
    expect(await screen.findByText('me@example.com')).toBeInTheDocument();
    expect(screen.getByText('admin')).toBeInTheDocument();
    expect(screen.getByLabelText(/display name/i)).toHaveValue('Original Name');
  });

  it('shows the failure message when the profile fetch fails', async () => {
    vi.mocked(fetchMe).mockRejectedValue(new Error('boom'));
    renderEditor();
    expect(await screen.findByText(/failed to load your profile/i)).toBeInTheDocument();
  });

  it('disables Save and Reset until the display name is dirty', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    expect(screen.getByRole('button', { name: /save changes/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /^reset$/i })).toBeDisabled();

    await userEvent.clear(input);
    await userEvent.type(input, 'New Name');
    expect(screen.getByRole('button', { name: /save changes/i })).toBeEnabled();
    expect(screen.getByRole('button', { name: /^reset$/i })).toBeEnabled();
  });

  it('reset restores the original display name and disables the buttons again', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    await userEvent.clear(input);
    await userEvent.type(input, 'New Name');
    await userEvent.click(screen.getByRole('button', { name: /^reset$/i }));
    expect(input).toHaveValue('Original Name');
    expect(screen.getByRole('button', { name: /save changes/i })).toBeDisabled();
  });

  it('submitting a blank display name sends null', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(updateProfile).mockResolvedValue({ ...sampleUser, displayName: null });
    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    await userEvent.clear(input);
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    await waitFor(() => {
      expect(vi.mocked(updateProfile)).toHaveBeenCalledWith({ displayName: null });
    });
    await waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith('Profile updated.');
    });
  });

  it('updates the auth store user displayName after a successful save', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(updateProfile).mockResolvedValue({ ...sampleUser, displayName: 'Renamed' });
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u-1',
      email: 'me@example.com',
      displayName: 'Original Name',
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });

    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    await userEvent.clear(input);
    await userEvent.type(input, 'Renamed');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(useAccessTokenStore.getState().user?.displayName).toBe('Renamed');
    });
  });

  it('renders the API detail message when saving the profile fails', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(updateProfile).mockRejectedValue(
      new ApiError(409, { detail: 'Name already taken.' }),
    );
    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    await userEvent.clear(input);
    await userEvent.type(input, 'Taken');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    expect(await screen.findByText('Name already taken.')).toBeInTheDocument();
  });

  it('falls back to a generic profile error when no detail is present', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(updateProfile).mockRejectedValue(new Error('net'));
    renderEditor();
    const input = await screen.findByLabelText(/display name/i);
    await userEvent.clear(input);
    await userEvent.type(input, 'Whatever');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    expect(await screen.findByText(/could not update your profile/i)).toBeInTheDocument();
  });

  it('toggles password visibility when the eye button is clicked', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    renderEditor();
    const newPassword = await screen.findByLabelText(/^new password$/i);
    expect(newPassword).toHaveAttribute('type', 'password');
    const showBtns = screen.getAllByRole('button', { name: /show password/i });
    await userEvent.click(showBtns[1]);
    expect(newPassword).toHaveAttribute('type', 'text');
  });

  it('keeps the change-password button disabled until all rules are met', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    renderEditor();
    await screen.findByLabelText(/current password/i);
    const submit = screen.getByRole('button', { name: /^change password$/i });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText(/current password/i), 'old-password');
    await userEvent.type(screen.getByLabelText(/^new password$/i), 'short');
    expect(await screen.findByText(/password is too short/i)).toBeInTheDocument();
    expect(submit).toBeDisabled();

    await userEvent.clear(screen.getByLabelText(/^new password$/i));
    await userEvent.type(screen.getByLabelText(/^new password$/i), 'a-good-password');
    await userEvent.type(screen.getByLabelText(/confirm new password/i), 'different-password');
    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument();
    expect(submit).toBeDisabled();
  });

  it('submits a valid password change and toasts success', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(changePassword).mockResolvedValue(undefined);
    renderEditor();
    await screen.findByLabelText(/current password/i);

    await userEvent.type(screen.getByLabelText(/current password/i), 'old-password');
    await userEvent.type(screen.getByLabelText(/^new password$/i), 'a-strong-new-pass');
    await userEvent.type(screen.getByLabelText(/confirm new password/i), 'a-strong-new-pass');
    await userEvent.click(screen.getByRole('button', { name: /^change password$/i }));

    await waitFor(() => {
      expect(vi.mocked(changePassword)).toHaveBeenCalledWith({
        currentPassword: 'old-password',
        newPassword: 'a-strong-new-pass',
      });
    });
    await waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith(
        'Password changed. Other sessions have been signed out.',
      );
    });
  });

  it('renders the API detail message when changing password fails', async () => {
    vi.mocked(fetchMe).mockResolvedValue(sampleUser);
    vi.mocked(changePassword).mockRejectedValue(
      new ApiError(400, { detail: 'Current password incorrect.' }),
    );
    renderEditor();
    await screen.findByLabelText(/current password/i);

    await userEvent.type(screen.getByLabelText(/current password/i), 'old-password');
    await userEvent.type(screen.getByLabelText(/^new password$/i), 'a-strong-new-pass');
    await userEvent.type(screen.getByLabelText(/confirm new password/i), 'a-strong-new-pass');
    await userEvent.click(screen.getByRole('button', { name: /^change password$/i }));

    expect(await screen.findByText('Current password incorrect.')).toBeInTheDocument();
  });
});
