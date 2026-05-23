import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceBindings } from '../components/licences/LicenceBindings';
import { ApiError } from '../auth/api-client';

vi.mock('../api/licences', () => ({
  updateLicenceHwid: vi.fn(),
  updateLicenceIpAllowlist: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { updateLicenceHwid, updateLicenceIpAllowlist } from '../api/licences';
import { toast } from 'sonner';

function makeLicence(over: Record<string, unknown> = {}) {
  return {
    id: 'lic-1',
    productId: 'p',
    productSlug: 's',
    userId: 'u',
    userEmail: 'a@b.com',
    status: 'active',
    expiresAt: null,
    notes: null,
    hwidBound: false,
    ipAllowlist: null,
    label: null,
    createdAt: '2026-01-01T00:00:00Z',
    ...over,
  };
}

function renderBindings(over: Record<string, unknown> = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceBindings licence={makeLicence(over)} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(updateLicenceHwid).mockReset();
  vi.mocked(updateLicenceIpAllowlist).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
});

describe('LicenceBindings extra coverage', () => {
  it('confirming Clear HWID calls updateLicenceHwid with null', async () => {
    vi.mocked(updateLicenceHwid).mockResolvedValue(undefined);
    renderBindings({ hwidBound: true });
    await userEvent.click(screen.getByRole('button', { name: /clear hwid/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /clear binding/i }));
    expect(vi.mocked(updateLicenceHwid)).toHaveBeenCalledWith('lic-1', { hwid: null, reason: null });
  });

  it('shows a success toast after clearing the HWID binding', async () => {
    vi.mocked(updateLicenceHwid).mockResolvedValue(undefined);
    renderBindings({ hwidBound: true });
    await userEvent.click(screen.getByRole('button', { name: /clear hwid/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /clear binding/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith('Hardware binding cleared.');
    });
  });

  it('shows the ApiError detail in a toast when clearing the HWID fails', async () => {
    vi.mocked(updateLicenceHwid).mockRejectedValue(new ApiError(409, { detail: 'Cannot clear binding.' }));
    renderBindings({ hwidBound: true });
    await userEvent.click(screen.getByRole('button', { name: /clear hwid/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /clear binding/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Cannot clear binding.');
    });
  });

  it('falls back to a generic error toast when clearing the HWID fails without an ApiError body', async () => {
    vi.mocked(updateLicenceHwid).mockRejectedValue(new Error('boom'));
    renderBindings({ hwidBound: true });
    await userEvent.click(screen.getByRole('button', { name: /clear hwid/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /clear binding/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Could not clear the hardware binding.');
    });
  });

  it('shows a success toast after saving the IP allowlist', async () => {
    vi.mocked(updateLicenceIpAllowlist).mockResolvedValue(undefined);
    renderBindings({ ipAllowlist: ['10.0.0.0/8'] });
    await userEvent.click(screen.getByRole('button', { name: /save allowlist/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith('IP allowlist saved.');
    });
  });

  it('shows the ApiError detail in a toast when saving the allowlist fails', async () => {
    vi.mocked(updateLicenceIpAllowlist).mockRejectedValue(new ApiError(422, { detail: 'Invalid CIDR.' }));
    renderBindings({ ipAllowlist: ['bad'] });
    await userEvent.click(screen.getByRole('button', { name: /save allowlist/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Invalid CIDR.');
    });
  });

  it('falls back to a generic toast when saving the allowlist fails without an ApiError body', async () => {
    vi.mocked(updateLicenceIpAllowlist).mockRejectedValue(new Error('boom'));
    renderBindings({ ipAllowlist: ['10.0.0.0/8'] });
    await userEvent.click(screen.getByRole('button', { name: /save allowlist/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Could not save the IP allowlist.');
    });
  });

  it('shows the auto-lock helper text when the allowlist is restricted', () => {
    renderBindings({ ipAllowlist: [] });
    expect(screen.getByText(/leave empty and the first ip/i)).toBeInTheDocument();
  });

  it('shows the off helper text when restriction is disabled', () => {
    renderBindings({ ipAllowlist: null });
    expect(screen.getByText(/ip restriction is off/i)).toBeInTheDocument();
  });
});
