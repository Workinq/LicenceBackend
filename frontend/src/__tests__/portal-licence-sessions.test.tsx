import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PortalLicenceSessions } from '../components/licences/PortalLicenceSessions';

vi.mock('../api/me-licences', () => ({ fetchMyLicenceSeats: vi.fn() }));
vi.mock('../api/checkouts', () => ({ checkinSeat: vi.fn() }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { fetchMyLicenceSeats } from '../api/me-licences';
import { checkinSeat } from '../api/checkouts';

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <PortalLicenceSessions licenceId="lic-1" />
    </QueryClientProvider>,
  );
}

const sampleSeat = {
  id: 'seat-1',
  instanceIdHashPrefix: 'aabbccdd',
  memberUserId: null,
  hwidHmacBase64: null,
  sourceIp: '10.0.0.1',
  issuedAt: '2026-05-19T12:00:00Z',
  lastHeartbeatAt: '2026-05-19T12:01:00Z',
  expiresAt: '2026-05-19T12:10:00Z',
};

const sampleResponse = {
  maxSeats: 5,
  live: [sampleSeat],
  history: { items: [], total: 0, limit: 20, offset: 0 },
};

beforeEach(() => {
  vi.mocked(fetchMyLicenceSeats).mockReset();
  vi.mocked(checkinSeat).mockReset();
});

describe('PortalLicenceSessions', () => {
  it('renders an active session row', async () => {
    vi.mocked(fetchMyLicenceSeats).mockResolvedValue(sampleResponse);
    renderPanel();
    await waitFor(() => expect(screen.getByText(/aabbccdd/)).toBeInTheDocument());
    expect(screen.getByText(/10\.0\.0\.1/)).toBeInTheDocument();
  });

  it('signs out a session after confirming', async () => {
    vi.mocked(fetchMyLicenceSeats).mockResolvedValue(sampleResponse);
    vi.mocked(checkinSeat).mockResolvedValue(undefined);

    renderPanel();
    await waitFor(() => expect(screen.getByText(/aabbccdd/)).toBeInTheDocument());

    await userEvent.click(screen.getByRole('button', { name: /^sign out$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /^sign out$/i }));

    expect(vi.mocked(checkinSeat)).toHaveBeenCalledWith('seat-1');
  });

  it('shows the empty state when there are no live sessions', async () => {
    vi.mocked(fetchMyLicenceSeats).mockResolvedValue({
      ...sampleResponse,
      live: [],
    });
    renderPanel();
    await waitFor(() => expect(screen.getByText(/no active sessions/i)).toBeInTheDocument());
  });
});
