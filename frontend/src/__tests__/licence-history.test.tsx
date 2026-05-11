import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceHistory } from '../components/licences/LicenceHistory';

vi.mock('../api/licences', () => ({
  fetchLicenceStatusHistory: vi.fn(),
  fetchLicenceBindingHistory: vi.fn(),
  fetchLicenceVerificationAttempts: vi.fn(),
}));
import { fetchLicenceStatusHistory, fetchLicenceBindingHistory, fetchLicenceVerificationAttempts } from '../api/licences';

function renderHistory() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceHistory licenceId="lic-1" />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchLicenceStatusHistory).mockReset();
  vi.mocked(fetchLicenceBindingHistory).mockReset();
  vi.mocked(fetchLicenceVerificationAttempts).mockReset();
  vi.mocked(fetchLicenceStatusHistory).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
  vi.mocked(fetchLicenceBindingHistory).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
  vi.mocked(fetchLicenceVerificationAttempts).mockResolvedValue({ items: [], total: 0, limit: 20, offset: 0 });
});

describe('LicenceHistory', () => {
  it('renders the three tabs', () => {
    renderHistory();
    expect(screen.getByRole('tab', { name: /status/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /binding/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /verification/i })).toBeInTheDocument();
  });

  it('shows the status history events in the default tab', async () => {
    vi.mocked(fetchLicenceStatusHistory).mockResolvedValue({
      items: [{ id: 'h1', previousStatus: 'active', newStatus: 'suspended', changedBy: 'u1', changedByEmail: 'admin@example.com', changedAt: '2026-01-01T00:00:00Z', reason: 'manual' }],
      total: 1, limit: 20, offset: 0,
    });
    renderHistory();
    expect(await screen.findByText(/active.*suspended/i)).toBeInTheDocument();
  });

  it('loads the binding history when its tab is clicked', async () => {
    vi.mocked(fetchLicenceBindingHistory).mockResolvedValue({
      items: [{ id: 'b1', bindingType: 'hwid', previousValue: null, newValue: 'fp123', changeSource: 'admin', changedByUserId: 'u1', changedAt: '2026-01-01T00:00:00Z', reason: null }],
      total: 1, limit: 20, offset: 0,
    });
    renderHistory();
    await userEvent.click(screen.getByRole('tab', { name: /binding/i }));
    expect(await screen.findByText(/hwid/i)).toBeInTheDocument();
  });
});
