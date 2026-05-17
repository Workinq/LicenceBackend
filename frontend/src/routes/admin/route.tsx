// frontend/src/routes/admin/route.tsx
import { createFileRoute, redirect } from '@tanstack/react-router';
import { useAccessTokenStore, type AuthUser } from '../../auth/access-token-store';
import { API_BASE } from '../../auth/api-client';
import { useSilentRefresh } from '../../auth/use-silent-refresh';
import { AppShell } from '../../components/layout/AppShell';

export const Route = createFileRoute('/admin')({
  beforeLoad: async () => {
    const store = useAccessTokenStore.getState();
    if (!store.accessToken) {
      try {
        const res = await fetch(`${API_BASE}/sessions/refresh`, {
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
    }

    const role = useAccessTokenStore.getState().user?.role;
    if (role !== 'admin') {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/portal' });
    }
  },
  component: AuthedLayout,
});

function AuthedLayout() {
  useSilentRefresh();
  return <AppShell />;
}
