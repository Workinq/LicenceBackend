import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceKeys } from '../components/licences/LicenceKeys';
import { ApiError } from '../auth/api-client';

vi.mock('../api/licence-keys', () => ({
  fetchLicenceKeys: vi.fn(),
  mintLicenceKey: vi.fn(),
  revokeLicenceKey: vi.fn(),
  updateLicenceKeyLabel: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import {
  fetchLicenceKeys,
  mintLicenceKey,
  revokeLicenceKey,
  updateLicenceKeyLabel,
} from '../api/licence-keys';
import { toast } from 'sonner';

function renderPanel(canMutate = true) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <LicenceKeys licenceId="lic-1" canMutate={canMutate} />
    </QueryClientProvider>,
  );
}

const activeKey = {
  id: 'key-1',
  licenceId: 'lic-1',
  keyPrefix: 'LIC-AAAA-BBBB',
  label: 'laptop',
  createdByUserId: 'user-1',
  createdAt: '2026-05-19T12:00:00Z',
  lastSeenAt: '2026-05-20T12:00:00Z',
  revokedAt: null,
  revokedByUserId: null,
  revokeReason: null,
};

const baseResponse = {
  activeCount: 1,
  activeCap: 5,
  keys: [activeKey],
};

beforeEach(() => {
  vi.mocked(fetchLicenceKeys).mockReset();
  vi.mocked(mintLicenceKey).mockReset();
  vi.mocked(revokeLicenceKey).mockReset();
  vi.mocked(updateLicenceKeyLabel).mockReset();
  vi.mocked(toast.success).mockReset();
  vi.mocked(toast.error).mockReset();
});

describe('LicenceKeys', () => {
  it('renders a skeleton while loading', () => {
    vi.mocked(fetchLicenceKeys).mockReturnValue(new Promise(() => undefined));
    const { container } = renderPanel();
    expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
  });

  it('renders the key list when data resolves', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue(baseResponse);
    renderPanel();
    expect(await screen.findByText('LIC-AAAA-BBBB')).toBeInTheDocument();
    expect(screen.getByText(/1\/5 active keys/i)).toBeInTheDocument();
    expect(screen.getByText('laptop')).toBeInTheDocument();
  });

  it('disables the generate button when at cap', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue({
      activeCount: 5,
      activeCap: 5,
      keys: [
        activeKey,
        { ...activeKey, id: 'key-2', keyPrefix: 'LIC-CCCC-DDDD' },
        { ...activeKey, id: 'key-3', keyPrefix: 'LIC-EEEE-FFFF' },
        { ...activeKey, id: 'key-4', keyPrefix: 'LIC-GGGG-HHHH' },
        { ...activeKey, id: 'key-5', keyPrefix: 'LIC-IIII-JJJJ' },
      ],
    });
    renderPanel();
    const button = await screen.findByRole('button', { name: /generate new key/i });
    expect(button).toBeDisabled();
  });

  it('hides the generate button when canMutate is false', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue(baseResponse);
    renderPanel(false);
    await screen.findByText('LIC-AAAA-BBBB');
    expect(screen.queryByRole('button', { name: /generate new key/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /revoke key/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit label/i })).not.toBeInTheDocument();
  });

  it('opens the reveal-once dialog after a successful mint', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue({
      activeCount: 0,
      activeCap: 5,
      keys: [],
    });
    vi.mocked(mintLicenceKey).mockResolvedValue({
      key: activeKey,
      licenceKey: 'LIC-RAW-NEW-PLAINTEXT',
    });

    renderPanel();
    await screen.findByText(/no active keys yet/i);

    await userEvent.click(screen.getByRole('button', { name: /generate new key/i }));

    expect(await screen.findByText('LIC-RAW-NEW-PLAINTEXT')).toBeInTheDocument();
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByRole('button', { name: /done/i })).toBeInTheDocument();
  });

  it('shows a cap-exceeded toast when the mint mutation returns the matching problem', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue(baseResponse);
    vi.mocked(mintLicenceKey).mockRejectedValue(
      new ApiError(409, { title: 'licence_key_cap_exceeded', detail: 'cap reached' }),
    );

    renderPanel();
    await userEvent.click(await screen.findByRole('button', { name: /generate new key/i }));

    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith(
        'Maximum active keys reached. Revoke one first.',
      );
    });
  });

  it('revokes a key after confirming the destructive prompt', async () => {
    vi.mocked(fetchLicenceKeys).mockResolvedValue(baseResponse);
    vi.mocked(revokeLicenceKey).mockResolvedValue({ ...activeKey, revokedAt: '2026-05-21T00:00:00Z' });

    renderPanel();
    await screen.findByText('LIC-AAAA-BBBB');

    await userEvent.click(screen.getByRole('button', { name: /^revoke key$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /^revoke key$/i }));

    await waitFor(() => {
      expect(vi.mocked(revokeLicenceKey)).toHaveBeenCalledWith('lic-1', 'key-1', null);
    });
  });
});
