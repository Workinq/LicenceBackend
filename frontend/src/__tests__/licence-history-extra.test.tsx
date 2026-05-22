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
vi.mock('../api/audit-events', () => ({ fetchAuditEvents: vi.fn() }));

import { fetchLicenceStatusHistory, fetchLicenceBindingHistory, fetchLicenceVerificationAttempts } from '../api/licences';
import { fetchAuditEvents } from '../api/audit-events';

function renderHistory() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceHistory licenceId="lic-1" />
    </QueryClientProvider>,
  );
}

const emptyPage = { items: [], total: 0, limit: 20, offset: 0 };

beforeEach(() => {
  vi.mocked(fetchLicenceStatusHistory).mockReset();
  vi.mocked(fetchLicenceBindingHistory).mockReset();
  vi.mocked(fetchLicenceVerificationAttempts).mockReset();
  vi.mocked(fetchAuditEvents).mockReset();
  vi.mocked(fetchLicenceStatusHistory).mockResolvedValue(emptyPage);
  vi.mocked(fetchLicenceBindingHistory).mockResolvedValue(emptyPage);
  vi.mocked(fetchLicenceVerificationAttempts).mockResolvedValue(emptyPage);
  vi.mocked(fetchAuditEvents).mockResolvedValue(emptyPage);
});

describe('LicenceHistory (extra)', () => {
  it('renders the members and key tabs in addition to the original three', () => {
    renderHistory();
    expect(screen.getByRole('tab', { name: /member history/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /key history/i })).toBeInTheDocument();
  });

  it('loads member-add and member-remove events when the Member tab is clicked', async () => {
    vi.mocked(fetchAuditEvents).mockResolvedValue({
      items: [
        { id: 'e1', occurredAt: '2026-01-01T00:00:00Z', eventType: 'licence.member_added', subjectType: 'licence', subjectId: 'lic-1', actorType: 'user', actorUserId: 'u1', actorUserEmail: 'admin@example.com', reason: null, payload: { memberEmail: 'alice@example.com' } },
        { id: 'e2', occurredAt: '2026-01-02T00:00:00Z', eventType: 'licence.member_removed', subjectType: 'licence', subjectId: 'lic-1', actorType: 'user', actorUserId: 'u1', actorUserEmail: 'admin@example.com', reason: 'cleanup', payload: { memberEmail: 'bob@example.com' } },
      ],
      total: 2, limit: 20, offset: 0,
    });
    renderHistory();
    await userEvent.click(screen.getByRole('tab', { name: /member history/i }));
    expect(await screen.findByText(/added alice@example.com/i)).toBeInTheDocument();
    expect(screen.getByText(/removed bob@example.com/i)).toBeInTheDocument();
    expect(vi.mocked(fetchAuditEvents).mock.calls[0][0]).toMatchObject({
      subject_type: 'licence',
      subject_id: 'lic-1',
      event_type: ['licence.member_added', 'licence.member_removed'],
    });
  });

  it('falls back to "unknown" when the member payload has no email', async () => {
    vi.mocked(fetchAuditEvents).mockResolvedValue({
      items: [
        { id: 'e3', occurredAt: '2026-01-01T00:00:00Z', eventType: 'licence.member_added', subjectType: 'licence', subjectId: 'lic-1', actorType: 'user', actorUserId: 'u1', actorUserEmail: null, reason: null, payload: null },
      ],
      total: 1, limit: 20, offset: 0,
    });
    renderHistory();
    await userEvent.click(screen.getByRole('tab', { name: /member history/i }));
    expect(await screen.findByText(/added unknown/i)).toBeInTheDocument();
  });

  it('loads key regeneration events on the Key tab', async () => {
    vi.mocked(fetchAuditEvents).mockResolvedValue({
      items: [
        { id: 'k1', occurredAt: '2026-01-03T00:00:00Z', eventType: 'licence.key_regenerated', subjectType: 'licence', subjectId: 'lic-1', actorType: 'user', actorUserId: 'u1', actorUserEmail: 'admin@example.com', reason: 'lost device', payload: {} },
      ],
      total: 1, limit: 20, offset: 0,
    });
    renderHistory();
    await userEvent.click(screen.getByRole('tab', { name: /key history/i }));
    expect(await screen.findByText(/licence key regenerated/i)).toBeInTheDocument();
    expect(vi.mocked(fetchAuditEvents).mock.calls.at(-1)?.[0]).toMatchObject({
      event_type: ['licence.key_regenerated'],
    });
  });

  it('shows pagination controls and advances offset when the total exceeds the page size', async () => {
    vi.mocked(fetchLicenceStatusHistory)
      .mockResolvedValueOnce({
        items: [{ id: 's1', previousStatus: 'active', newStatus: 'suspended', changedBy: 'u1', changedByEmail: 'admin@example.com', changedAt: '2026-01-01T00:00:00Z', reason: null }],
        total: 50, limit: 20, offset: 0,
      })
      .mockResolvedValue({
        items: [{ id: 's21', previousStatus: 'suspended', newStatus: 'active', changedBy: 'u1', changedByEmail: 'admin@example.com', changedAt: '2026-01-02T00:00:00Z', reason: null }],
        total: 50, limit: 20, offset: 20,
      });
    renderHistory();
    expect(await screen.findByText(/active.*suspended/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled();
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    expect(vi.mocked(fetchLicenceStatusHistory).mock.calls.at(-1)?.[1]).toEqual({ limit: 20, offset: 20 });
  });

  it('renders the verification outcome with the denial reason in the title', async () => {
    vi.mocked(fetchLicenceVerificationAttempts).mockResolvedValue({
      items: [
        { id: 'v1', outcome: 'denied', denialReason: 'hwid_mismatch', sourceIp: '10.0.0.1', hwidFingerprint: 'fp123', attemptedAt: '2026-01-01T00:00:00Z' },
      ],
      total: 1, limit: 20, offset: 0,
    });
    renderHistory();
    await userEvent.click(screen.getByRole('tab', { name: /verification attempts/i }));
    expect(await screen.findByText(/denied: hwid_mismatch/i)).toBeInTheDocument();
    expect(screen.getByText(/10\.0\.0\.1 - HWID fp123/i)).toBeInTheDocument();
  });
});
