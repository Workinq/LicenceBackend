import { createFileRoute, redirect } from '@tanstack/react-router';
import { useAccessTokenStore, type AuthUser } from '@/auth/access-token-store';
import { API_BASE } from '@/auth/api-client';

export const Route = createFileRoute('/')({
  beforeLoad: async () => {
    const store = useAccessTokenStore.getState();
    let role = store.user?.role;

    if (!store.accessToken) {
      try {
        const res = await fetch(`${API_BASE}/sessions/refresh`, {
          method: 'POST',
          credentials: 'include',
        });

        if (res.ok) {
          const body = (await res.json()) as {
            accessToken: string;
            accessTokenExpiresAt: string;
            user: AuthUser;
          };
          store.setSession(body.accessToken, new Date(body.accessTokenExpiresAt), body.user);
          role = body.user.role;
        }
      } catch {
        // fall through to /login
      }
    }

    // eslint-disable-next-line @typescript-eslint/only-throw-error
    throw redirect({ to: role === 'admin' ? '/admin' : role ? '/portal' : '/login' });
  },
  component: () => null,
});
