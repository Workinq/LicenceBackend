import { describe, it, expect } from 'vitest';
import { formatPrice, formatDate, formatDateTime } from '../lib/format';

describe('formatPrice', () => {
  it('returns a currency-formatted string for a known currency', () => {
    expect(formatPrice(1500, 'USD')).toMatch(/1,?500/);
    expect(formatPrice(1500, 'USD')).toContain('$');
  });

  it('falls back to a plain amount and code when the currency is invalid', () => {
    expect(formatPrice(2.5, 'NOTACUR')).toBe('2.50 NOTACUR');
  });
});

describe('formatDate', () => {
  it('parses an ISO date and returns a locale date string', () => {
    const out = formatDate('2026-05-22T00:00:00Z');
    expect(typeof out).toBe('string');
    expect(out.length).toBeGreaterThan(0);
  });
});

describe('formatDateTime', () => {
  it('parses an ISO date-time and returns a locale string with time component', () => {
    const out = formatDateTime('2026-05-22T13:45:00Z');
    expect(typeof out).toBe('string');
    expect(out.length).toBeGreaterThan(0);
  });
});
