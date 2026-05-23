import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { Route as IndexRoute } from '../routes/index';
import { useAccessTokenStore } from '../auth/access-token-store';

type RedirectResponse = Response & { options: { to?: string } };

async function callBeforeLoad(): Promise<RedirectResponse | undefined> {
  const beforeLoad = IndexRoute.options.beforeLoad as
    | ((ctx: unknown) => Promise<unknown> | unknown)
    | undefined;
  if (!beforeLoad) return undefined;
  try {
    await beforeLoad({ context: {}, location: { pathname: '/' } } as never);
    return undefined;
  } catch (err) {
    return err as RedirectResponse;
  }
}

beforeEach(() => {
  useAccessTokenStore.getState().clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
  useAccessTokenStore.getState().clear();
});

describe('RootIndexRoute', () => {
  it('redirects an authenticated admin to /admin', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'admin@example.com',
      displayName: 'Admin',
      role: 'admin',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    const redirect = await callBeforeLoad();
    expect(redirect?.options.to).toBe('/admin');
  });

  it('redirects an authenticated non-admin user to /portal', async () => {
    useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
      id: 'u1',
      email: 'user@example.com',
      displayName: 'User',
      role: 'user',
      status: 'active',
      createdAt: '2026-01-01T00:00:00Z',
    });
    const redirect = await callBeforeLoad();
    expect(redirect?.options.to).toBe('/portal');
  });

  it('attempts a silent refresh and redirects to /admin when it returns an admin session', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        json: () =>
          Promise.resolve({
            accessToken: 'tok',
            accessTokenExpiresAt: new Date(Date.now() + 900_000).toISOString(),
            user: {
              id: 'u1',
              email: 'admin@example.com',
              displayName: null,
              role: 'admin',
              status: 'active',
              createdAt: '2026-01-01T00:00:00Z',
            },
          }),
      }),
    );
    const redirect = await callBeforeLoad();
    expect(redirect?.options.to).toBe('/admin');
    expect(useAccessTokenStore.getState().accessToken).toBe('tok');
  });

  it('redirects to /login when there is no session and refresh fails', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));
    const redirect = await callBeforeLoad();
    expect(redirect?.options.to).toBe('/login');
  });

  it('redirects to /login when refresh throws', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network')));
    const redirect = await callBeforeLoad();
    expect(redirect?.options.to).toBe('/login');
  });

  it('exposes a null-rendering component so the route never paints UI', () => {
    const Component = IndexRoute.options.component as () => null;
    expect(Component()).toBeNull();
  });
});
