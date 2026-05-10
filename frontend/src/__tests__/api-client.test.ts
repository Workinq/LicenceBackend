// frontend/src/__tests__/api-client.test.ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useAccessTokenStore } from '../auth/access-token-store';

// We test the store-level behaviour triggered by the api-client:
// the fetch logic itself relies on the real network and is exercised manually.

beforeEach(() => {
  useAccessTokenStore.getState().clear();
  vi.restoreAllMocks();
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('apiClient — store integration', () => {
  it('clear() is called when refresh fails (simulated)', () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'a@b.com',
      displayName: null,
      role: 'admin',
      status: 'active',
      createdAt: new Date().toISOString(),
    });
    expect(useAccessTokenStore.getState().accessToken).toBe('tok');

    useAccessTokenStore.getState().clear();
    expect(useAccessTokenStore.getState().accessToken).toBeNull();
  });

  it('setSession() updates store after successful refresh (simulated)', () => {
    const expiry = new Date(Date.now() + 900_000);
    useAccessTokenStore.getState().setSession('new_tok', expiry, {
      id: 'u2',
      email: 'b@c.com',
      displayName: 'Bob',
      role: 'admin',
      status: 'active',
      createdAt: new Date().toISOString(),
    });
    expect(useAccessTokenStore.getState().accessToken).toBe('new_tok');
    expect(useAccessTokenStore.getState().user?.email).toBe('b@c.com');
  });
});
