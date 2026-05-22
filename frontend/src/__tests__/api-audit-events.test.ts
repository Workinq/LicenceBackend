import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/auth/api-client', () => ({
  apiClient: vi.fn(),
}));

import { apiClient } from '@/auth/api-client';
import { fetchAuditEvents } from '../api/audit-events';

beforeEach(() => {
  vi.mocked(apiClient).mockReset();
});

function lastCallUrl(): string {
  const calls = vi.mocked(apiClient).mock.calls;
  return calls[calls.length - 1][0] as string;
}

describe('api/audit-events', () => {
  it('returns the unwrapped data when apiClient resolves', async () => {
    const page = { items: [], total: 0, limit: 20, offset: 0 };
    vi.mocked(apiClient).mockResolvedValue({ data: page, status: 200, headers: new Headers() } as never);
    expect(await fetchAuditEvents({ limit: 20 } as never)).toEqual(page);
  });

  it('issues a GET against the audit-events route', async () => {
    vi.mocked(apiClient).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 }, status: 200, headers: new Headers() } as never);
    await fetchAuditEvents({} as never);
    expect(apiClient).toHaveBeenCalledTimes(1);
    const [, init] = vi.mocked(apiClient).mock.calls[0];
    expect((init as RequestInit).method).toBe('GET');
    expect(lastCallUrl()).toBe('/audit-events');
  });

  it('serialises scalar params as a single query key', async () => {
    vi.mocked(apiClient).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 }, status: 200, headers: new Headers() } as never);
    await fetchAuditEvents({ limit: 25, offset: 50 } as never);
    const url = lastCallUrl();
    expect(url.startsWith('/audit-events?')).toBe(true);
    const params = new URLSearchParams(url.split('?')[1]);
    expect(params.get('limit')).toBe('25');
    expect(params.get('offset')).toBe('50');
  });

  it('repeats array params as separate query keys instead of CSV', async () => {
    vi.mocked(apiClient).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 }, status: 200, headers: new Headers() } as never);
    await fetchAuditEvents({ event_type: ['a', 'b'] } as never);
    const url = lastCallUrl();
    const params = new URLSearchParams(url.split('?')[1]);
    expect(params.getAll('event_type')).toEqual(['a', 'b']);
    expect(url).not.toContain('a%2Cb');
  });

  it('skips null and undefined params entirely', async () => {
    vi.mocked(apiClient).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 }, status: 200, headers: new Headers() } as never);
    await fetchAuditEvents({ limit: 10, offset: undefined, event_type: null } as never);
    const url = lastCallUrl();
    expect(url).toContain('limit=10');
    expect(url).not.toContain('offset=');
    expect(url).not.toContain('event_type=');
  });

  it('propagates errors raised by apiClient', async () => {
    vi.mocked(apiClient).mockRejectedValue(new Error('boom'));
    await expect(fetchAuditEvents({} as never)).rejects.toThrow('boom');
  });
});
