// frontend/src/auth/use-silent-refresh.ts
import { useEffect, useRef } from 'react';
import { useAccessTokenStore, type AuthUser } from './access-token-store';

/** 14 minutes in milliseconds  - just inside the 15-min access token TTL. */
const SILENT_REFRESH_INTERVAL_MS = 14 * 60 * 1000;

interface RefreshBody {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
}

async function doRefresh(): Promise<void> {
  try {
    const res = await fetch('/sessions/refresh', {
      method: 'POST',
      credentials: 'include',
    });

    if (!res.ok) {
      useAccessTokenStore.getState().clear();
      window.location.assign('/login');
      return;
    }

    const body = (await res.json()) as RefreshBody;
    useAccessTokenStore
      .getState()
      .setSession(body.accessToken, new Date(body.accessTokenExpiresAt), body.user);
  } catch {
    // Network error  - leave the session intact and try again on the next tick.
  }
}

export function useSilentRefresh(): void {
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const startInterval = () => {
    if (intervalRef.current !== null) return;
    intervalRef.current = setInterval(() => {
      if (document.visibilityState === 'hidden') return;
      void doRefresh();
    }, SILENT_REFRESH_INTERVAL_MS);
  };

  const stopInterval = () => {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  };

  useEffect(() => {
    startInterval();

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        void doRefresh();
        startInterval();
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      stopInterval();
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
}
