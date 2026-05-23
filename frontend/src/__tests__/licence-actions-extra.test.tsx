import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceActions } from '../components/licences/LicenceActions';
import { ApiError } from '../auth/api-client';

vi.mock('../api/licences', () => ({
  updateLicenceStatus: vi.fn(),
  regenerateLicenceKey: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { updateLicenceStatus } from '../api/licences';
import { toast } from 'sonner';

function makeLicence(status: string) {
  return {
    id: 'lic-1',
    productId: 'p',
    productSlug: 's',
    userId: 'u',
    userEmail: 'a@b.com',
    status,
    expiresAt: null,
    notes: null,
    hwidBound: false,
    ipAllowlist: null,
    label: null,
    createdAt: '2026-01-01T00:00:00Z',
  };
}

function renderActions(status: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceActions licence={makeLicence(status)} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(updateLicenceStatus).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
});

describe('LicenceActions mutation flow', () => {
  it('confirming Suspend calls updateLicenceStatus with suspended', async () => {
    vi.mocked(updateLicenceStatus).mockResolvedValue({} as never);
    renderActions('active');
    await userEvent.click(screen.getByRole('button', { name: /^suspend$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /suspend licence/i }));
    expect(vi.mocked(updateLicenceStatus)).toHaveBeenCalledWith('lic-1', { status: 'suspended', reason: null });
  });

  it('confirming Revoke from an active licence calls updateLicenceStatus with revoked', async () => {
    vi.mocked(updateLicenceStatus).mockResolvedValue({} as never);
    renderActions('active');
    await userEvent.click(screen.getByRole('button', { name: /^revoke$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /revoke licence/i }));
    expect(vi.mocked(updateLicenceStatus)).toHaveBeenCalledWith('lic-1', { status: 'revoked', reason: null });
  });

  it('clicking Reinstate on a suspended licence calls updateLicenceStatus with active', async () => {
    vi.mocked(updateLicenceStatus).mockResolvedValue({} as never);
    renderActions('suspended');
    await userEvent.click(screen.getByRole('button', { name: /reinstate/i }));
    expect(vi.mocked(updateLicenceStatus)).toHaveBeenCalledWith('lic-1', { status: 'active', reason: null });
  });

  it('shows a success toast after a successful status change', async () => {
    vi.mocked(updateLicenceStatus).mockResolvedValue({} as never);
    renderActions('suspended');
    await userEvent.click(screen.getByRole('button', { name: /reinstate/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.success)).toHaveBeenCalledWith('Licence updated.');
    });
  });

  it('shows the ApiError detail in a toast when the mutation fails', async () => {
    vi.mocked(updateLicenceStatus).mockRejectedValue(new ApiError(409, { detail: 'Licence already revoked.' }));
    renderActions('suspended');
    await userEvent.click(screen.getByRole('button', { name: /reinstate/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Licence already revoked.');
    });
  });

  it('shows a generic error toast when the mutation fails without an ApiError body', async () => {
    vi.mocked(updateLicenceStatus).mockRejectedValue(new Error('network down'));
    renderActions('suspended');
    await userEvent.click(screen.getByRole('button', { name: /reinstate/i }));
    await vi.waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Could not update the licence.');
    });
  });

  it('renders a fallback note for an unknown licence status', () => {
    renderActions('expired');
    expect(screen.getByText(/no actions available for status expired/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /suspend|reinstate|revoke/i })).not.toBeInTheDocument();
  });
});
