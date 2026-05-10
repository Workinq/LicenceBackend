// frontend/src/__tests__/access-token-store.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useAccessTokenStore } from '../auth/access-token-store';

beforeEach(() => {
  useAccessTokenStore.getState().clear();
});

describe('useAccessTokenStore', () => {
  it('starts with null state', () => {
    const { accessToken, expiresAt, user } = useAccessTokenStore.getState();
    expect(accessToken).toBeNull();
    expect(expiresAt).toBeNull();
    expect(user).toBeNull();
  });

  it('setSession populates all fields', () => {
    const expiresAt = new Date(Date.now() + 15 * 60 * 1000);
    const user = {
      id: 'u1',
      email: 'admin@example.com',
      displayName: null,
      role: 'admin' as const,
      status: 'active' as const,
      createdAt: new Date().toISOString(),
    };
    useAccessTokenStore.getState().setSession('tok_abc', expiresAt, user);

    const state = useAccessTokenStore.getState();
    expect(state.accessToken).toBe('tok_abc');
    expect(state.expiresAt).toBe(expiresAt.getTime());
    expect(state.user?.email).toBe('admin@example.com');
  });

  it('clear resets all fields to null', () => {
    const expiresAt = new Date(Date.now() + 900_000);
    useAccessTokenStore.getState().setSession('tok_xyz', expiresAt, {
      id: 'u2',
      email: 'a@b.com',
      displayName: null,
      role: 'admin',
      status: 'active',
      createdAt: new Date().toISOString(),
    });
    useAccessTokenStore.getState().clear();

    const { accessToken, expiresAt: exp, user } = useAccessTokenStore.getState();
    expect(accessToken).toBeNull();
    expect(exp).toBeNull();
    expect(user).toBeNull();
  });
});
