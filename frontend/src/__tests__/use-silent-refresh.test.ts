// frontend/src/__tests__/use-silent-refresh.test.ts
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSilentRefresh } from '../auth/use-silent-refresh';

const REFRESH_MS = 14 * 60 * 1000;

beforeEach(() => {
  vi.useFakeTimers();
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: true,
      json: () => ({
        accessToken: 'new_tok',
        accessTokenExpiresAt: new Date(Date.now() + 900_000).toISOString(),
        user: {
          id: 'u1',
          email: 'a@b.com',
          displayName: null,
          role: 'admin',
          status: 'active',
          createdAt: new Date().toISOString(),
        },
      }),
    }),
  );
  Object.defineProperty(document, 'visibilityState', { value: 'visible', writable: true });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('useSilentRefresh', () => {
  it('does not fire fetch immediately on mount', () => {
    renderHook(() => useSilentRefresh());
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  it('fires fetch after 14 minutes', async () => {
    renderHook(() => useSilentRefresh());

    await act(async () => {
      vi.advanceTimersByTime(REFRESH_MS);
    });

    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      '/sessions/refresh',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('skips the interval tick when document is hidden', async () => {
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', writable: true });
    renderHook(() => useSilentRefresh());

    await act(async () => {
      vi.advanceTimersByTime(REFRESH_MS);
    });

    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  it('clears the interval on unmount', async () => {
    const { unmount } = renderHook(() => useSilentRefresh());
    unmount();

    await act(async () => {
      vi.advanceTimersByTime(REFRESH_MS * 3);
    });

    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });
});
