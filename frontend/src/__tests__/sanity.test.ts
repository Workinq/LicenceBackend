import { describe, it, expect } from 'vitest';

describe('toolchain sanity', () => {
  it('has access to vitest globals', () => {
    expect(typeof describe).toBe('function');
    expect(typeof it).toBe('function');
    expect(typeof expect).toBe('function');
  });
});
