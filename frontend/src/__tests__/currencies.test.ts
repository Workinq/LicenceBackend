import { describe, it, expect, afterEach, vi } from 'vitest';
import { fetchCurrencyList, FALLBACK_CURRENCIES } from '../api/currencies';

afterEach(() => {
  vi.restoreAllMocks();
});

describe('fetchCurrencyList', () => {
  it('returns the parsed JSON body when the CDN responds 200', async () => {
    const body = { usd: 'us dollar', eur: 'euro' };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => body }));
    expect(await fetchCurrencyList()).toEqual(body);
  });

  it('throws with the status code when the CDN returns a non-ok response', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 503, json: async () => ({}) }));
    await expect(fetchCurrencyList()).rejects.toThrow(/503/);
  });
});

describe('FALLBACK_CURRENCIES', () => {
  it('lists the canonical fiat fallbacks with code and human-readable name', () => {
    expect(FALLBACK_CURRENCIES.find((c) => c.code === 'USD')?.name).toBe('United States Dollar');
    expect(FALLBACK_CURRENCIES.find((c) => c.code === 'EUR')).toBeDefined();
    expect(FALLBACK_CURRENCIES.length).toBeGreaterThanOrEqual(10);
  });
});
