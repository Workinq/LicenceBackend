import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceMembers } from '../components/licences/LicenceMembers';
import { ApiError } from '../auth/api-client';

vi.mock('../api/licences', () => ({
  fetchLicenceMembers: vi.fn(),
  addLicenceMember: vi.fn(),
  removeLicenceMember: vi.fn(),
}));

import {
  fetchLicenceMembers,
  addLicenceMember,
  removeLicenceMember,
} from '../api/licences';

function renderPanel(licenceId = 'lic-1') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceMembers licenceId={licenceId} />
    </QueryClientProvider>,
  );
}

const sampleMember = {
  userId: 'u-1',
  email: 'member@example.com',
  addedByEmail: 'admin@example.com',
  addedAt: '2026-05-01T00:00:00Z',
};

beforeEach(() => {
  vi.mocked(fetchLicenceMembers).mockReset();
  vi.mocked(addLicenceMember).mockReset();
  vi.mocked(removeLicenceMember).mockReset();
});

describe('LicenceMembers', () => {
  it('renders the empty state when there are no members', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
    renderPanel();
    expect(await screen.findByText(/no members yet/i)).toBeInTheDocument();
  });

  it('renders an error message when loading fails', async () => {
    vi.mocked(fetchLicenceMembers).mockRejectedValue(new Error('boom'));
    renderPanel();
    expect(await screen.findByText(/failed to load members/i)).toBeInTheDocument();
  });

  it('renders a row for each member with their added-by metadata', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([sampleMember]);
    renderPanel();
    expect(await screen.findByText('member@example.com')).toBeInTheDocument();
    expect(screen.getByText(/added by admin@example\.com/i)).toBeInTheDocument();
  });

  it('falls back to "unknown" when addedByEmail is null', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([{ ...sampleMember, addedByEmail: null }]);
    renderPanel();
    await screen.findByText('member@example.com');
    expect(screen.getByText(/added by unknown/i)).toBeInTheDocument();
  });

  it('disables the Add button while the email is empty', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
    renderPanel();
    await screen.findByText(/no members yet/i);
    expect(screen.getByRole('button', { name: /^add$/i })).toBeDisabled();
  });

  it('submits a trimmed email when adding a member', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
    vi.mocked(addLicenceMember).mockResolvedValue(sampleMember);
    renderPanel('lic-7');
    await screen.findByText(/no members yet/i);

    await userEvent.type(screen.getByLabelText(/add member by email/i), '  new@example.com  ');
    await userEvent.click(screen.getByRole('button', { name: /^add$/i }));

    await waitFor(() => {
      expect(vi.mocked(addLicenceMember)).toHaveBeenCalledWith('lic-7', { email: 'new@example.com' });
    });
  });

  it('shows the API detail message when adding fails', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
    vi.mocked(addLicenceMember).mockRejectedValue(
      new ApiError(409, { detail: 'Already a member.' }),
    );
    renderPanel();
    await screen.findByText(/no members yet/i);

    await userEvent.type(screen.getByLabelText(/add member by email/i), 'dup@example.com');
    await userEvent.click(screen.getByRole('button', { name: /^add$/i }));

    expect(await screen.findByText('Already a member.')).toBeInTheDocument();
  });

  it('falls back to a generic add error when no detail is present', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([]);
    vi.mocked(addLicenceMember).mockRejectedValue(new Error('network'));
    renderPanel();
    await screen.findByText(/no members yet/i);

    await userEvent.type(screen.getByLabelText(/add member by email/i), 'x@example.com');
    await userEvent.click(screen.getByRole('button', { name: /^add$/i }));

    expect(await screen.findByText(/could not add the member/i)).toBeInTheDocument();
  });

  it('removes a member after confirming the destructive dialog', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([sampleMember]);
    vi.mocked(removeLicenceMember).mockResolvedValue(undefined);
    renderPanel('lic-2');
    await screen.findByText('member@example.com');

    await userEvent.click(screen.getByRole('button', { name: /remove member@example\.com/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /^remove$/i }));

    await waitFor(() => {
      expect(vi.mocked(removeLicenceMember)).toHaveBeenCalledWith('lic-2', 'u-1');
    });
  });

  it('shows the API detail message when removing fails', async () => {
    vi.mocked(fetchLicenceMembers).mockResolvedValue([sampleMember]);
    vi.mocked(removeLicenceMember).mockRejectedValue(
      new ApiError(403, { detail: 'Cannot remove owner.' }),
    );
    renderPanel();
    await screen.findByText('member@example.com');

    await userEvent.click(screen.getByRole('button', { name: /remove member@example\.com/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /^remove$/i }));

    expect(await screen.findByText('Cannot remove owner.')).toBeInTheDocument();
  });
});
