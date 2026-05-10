// frontend/src/routes/_authed/route.tsx
import { createFileRoute, redirect, Outlet } from '@tanstack/react-router';
import { useAccessTokenStore, type AuthUser } from '../../auth/access-token-store';
import { useSilentRefresh } from '../../auth/use-silent-refresh';

export const Route = createFileRoute('/_authed')({
  beforeLoad: async () => {
    const store = useAccessTokenStore.getState();
    if (store.accessToken) return;

    try {
      const res = await fetch('/sessions/refresh', {
        method: 'POST',
        credentials: 'include',
      });

      if (!res.ok) {
        // eslint-disable-next-line @typescript-eslint/only-throw-error
        throw redirect({ to: '/login' });
      }

      const body = (await res.json()) as {
        accessToken: string;
        accessTokenExpiresAt: string;
        user: AuthUser;
      };
      store.setSession(body.accessToken, new Date(body.accessTokenExpiresAt), body.user);
    } catch (err) {
      if (err instanceof Response || (err as { _isRedirect?: boolean })?._isRedirect) throw err;
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/login' });
    }
  },
  component: AuthedLayout,
});

function AuthedLayout() {
  useSilentRefresh();
  const user = useAccessTokenStore((s) => s.user);

  const handleSignOut = async () => {
    await fetch('/sessions', { method: 'DELETE', credentials: 'include' });
    useAccessTokenStore.getState().clear();
    window.location.assign('/login');
  };

  const handleSignOutAll = async () => {
    await fetch('/sessions/all', { method: 'DELETE', credentials: 'include' });
    useAccessTokenStore.getState().clear();
    window.location.assign('/login');
  };

  return (
    <div className="min-h-screen bg-surface font-sans text-ink">
      <header className="flex items-center justify-between border-b border-border px-6 py-3">
        <span className="font-display text-lg font-semibold">LicenceBackend Admin</span>
        <div className="flex items-center gap-3">
          {user && <span className="text-sm text-ink-muted">{user.email}</span>}
          <button
            onClick={() => { void handleSignOut(); }}
            className="rounded px-3 py-1.5 text-sm bg-ink text-surface-elevated hover:opacity-90"
          >
            Sign out
          </button>
          <button
            onClick={() => { void handleSignOutAll(); }}
            className="rounded px-3 py-1.5 text-sm border border-border text-ink-muted hover:bg-surface-sunken"
          >
            Sign out everywhere
          </button>
        </div>
      </header>
      <main className="p-6">
        <Outlet />
      </main>
    </div>
  );
}
