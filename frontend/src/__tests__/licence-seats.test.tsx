import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceSeats } from '../components/licences/LicenceSeats';

vi.mock('../api/licences', () => ({
  fetchLicenceSeats: vi.fn(),
  forceRevokeSeat: vi.fn(),
  updateLicenceMaxSeats: vi.fn(),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { fetchLicenceSeats, forceRevokeSeat, updateLicenceMaxSeats } from '../api/licences';

function renderPanel() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <LicenceSeats licenceId="lic-1" />
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
  vi.mocked(fetchLicenceSeats).mockReset();
  vi.mocked(forceRevokeSeat).mockReset();
  vi.mocked(updateLicenceMaxSeats).mockReset();
});

describe('LicenceSeats', () => {
  it('renders the live count and the seat row', async () => {
    vi.mocked(fetchLicenceSeats).mockResolvedValue(sampleResponse);
    renderPanel();
    await waitFor(() => expect(screen.getByText(/1 of 5/i)).toBeInTheDocument());
    expect(screen.getByText(/aabbccdd/)).toBeInTheDocument();
    expect(screen.getByText(/10\.0\.0\.1/)).toBeInTheDocument();
  });

  it('shows the empty state when there are no live seats', async () => {
    vi.mocked(fetchLicenceSeats).mockResolvedValue({
      ...sampleResponse,
      live: [],
    });
    renderPanel();
    await waitFor(() => expect(screen.getByText(/no active seats/i)).toBeInTheDocument());
  });

  it('revokes a seat after confirming', async () => {
    vi.mocked(fetchLicenceSeats).mockResolvedValue(sampleResponse);
    vi.mocked(forceRevokeSeat).mockResolvedValue(undefined);

    renderPanel();
    await waitFor(() => expect(screen.getByText(/aabbccdd/)).toBeInTheDocument());

    await userEvent.click(screen.getByRole('button', { name: /^revoke seat$/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /^revoke seat$/i }));

    expect(vi.mocked(forceRevokeSeat)).toHaveBeenCalledWith('lic-1', 'seat-1');
  });

  it('saves a new max-seats value', async () => {
    vi.mocked(fetchLicenceSeats).mockResolvedValue({
      ...sampleResponse,
      live: [],
    });
    vi.mocked(updateLicenceMaxSeats).mockResolvedValue({} as never);

    renderPanel();

    const input = await screen.findByLabelText(/max seats/i);
    await userEvent.clear(input);
    await userEvent.type(input, '10');
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect(vi.mocked(updateLicenceMaxSeats)).toHaveBeenCalledWith('lic-1', { maxSeats: 10, reason: null });
  });
});
