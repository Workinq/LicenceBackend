import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { apiClient, authedFetch, ApiError } from '../auth/api-client';
import { useAccessTokenStore } from '../auth/access-token-store';

const originalLocation = window.location;
const assignMock = vi.fn();

function jsonResponse(status: number, body: unknown, headers?: Record<string, string>): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(headers ?? {}),
    json: async () => body,
  } as unknown as Response;
}

function noBodyResponse(status: number): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(),
    json: async () => { throw new Error('no body'); },
  } as unknown as Response;
}

beforeEach(() => {
  useAccessTokenStore.getState().clear();
  Object.defineProperty(window, 'location', {
    value: { assign: assignMock },
    writable: true,
    configurable: true,
  });
  assignMock.mockReset();
});

afterEach(() => {
  vi.restoreAllMocks();
  useAccessTokenStore.getState().clear();
  Object.defineProperty(window, 'location', {
    value: originalLocation,
    writable: true,
    configurable: true,
  });
});

describe('apiClient and authedFetch', () => {
  it('attaches a Bearer token from the store when present', async () => {
    useAccessTokenStore.getState().setSession('tok-1', new Date(Date.now() + 900_000), {
      id: 'u1', email: 'a@b.com', displayName: null, role: 'admin', status: 'active', createdAt: new Date().toISOString(),
    });
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(200, { hello: 'world' }));
    const res = await apiClient<{ data: unknown; status: number; headers: Headers }>('/ping');
    expect(res.data).toEqual({ hello: 'world' });
    expect(res.status).toBe(200);
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer tok-1');
  });

  it('omits the Authorization header when no token is set', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(200, {}));
    await apiClient('/anon');
    const init = fetchMock.mock.calls[0][1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it('returns undefined data for 204 No Content responses', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(noBodyResponse(204));
    const res = await apiClient<{ data: unknown; status: number }>('/empty', { method: 'DELETE' });
    expect(res.data).toBeUndefined();
    expect(res.status).toBe(204);
  });

  it('throws an ApiError with the parsed body on 5xx', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(500, { error: 'boom' }));
    await expect(apiClient('/fail')).rejects.toMatchObject({
      status: 500,
      body: { error: 'boom' },
    });
  });

  it('throws an ApiError with null body when the error body is not json', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(noBodyResponse(503));
    const err = await apiClient('/maintenance').catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(503);
    expect((err as ApiError).body).toBeNull();
  });

  it('refreshes once on 401 and retries the original call', async () => {
    useAccessTokenStore.getState().setSession('old-tok', new Date(Date.now() + 900_000), {
      id: 'u1', email: 'a@b.com', displayName: null, role: 'admin', status: 'active', createdAt: new Date().toISOString(),
    });
    const fetchMock = vi.spyOn(global, 'fetch')
      .mockResolvedValueOnce(jsonResponse(401, { error: 'expired' }))
      .mockResolvedValueOnce(jsonResponse(200, {
        accessToken: 'new-tok',
        accessTokenExpiresAt: new Date(Date.now() + 900_000).toISOString(),
        user: { id: 'u1', email: 'a@b.com', displayName: null, role: 'admin', status: 'active', createdAt: new Date().toISOString() },
      }))
      .mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    const res = await apiClient<{ data: unknown }>('/secure');
    expect(res.data).toEqual({ ok: true });
    expect(useAccessTokenStore.getState().accessToken).toBe('new-tok');

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1][0]).toBe('/api/sessions/refresh');
    const retryInit = fetchMock.mock.calls[2][1] as RequestInit;
    expect((retryInit.headers as Record<string, string>).Authorization).toBe('Bearer new-tok');
  });

  it('clears the store and redirects to /login when refresh fails', async () => {
    useAccessTokenStore.getState().setSession('old-tok', new Date(Date.now() + 900_000), {
      id: 'u1', email: 'a@b.com', displayName: null, role: 'admin', status: 'active', createdAt: new Date().toISOString(),
    });
    vi.spyOn(global, 'fetch')
      .mockResolvedValueOnce(jsonResponse(401, null))
      .mockResolvedValueOnce(jsonResponse(401, null));

    await expect(apiClient('/secure')).rejects.toMatchObject({ status: 401 });
    expect(useAccessTokenStore.getState().accessToken).toBeNull();
    expect(assignMock).toHaveBeenCalledWith('/login');
  });

  it('does not attempt to refresh a 401 from /sessions/refresh itself', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(401, null));
    const res = await authedFetch('/sessions/refresh', { method: 'POST' });
    expect(res.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(assignMock).not.toHaveBeenCalled();
  });

  it('returns the raw Response from authedFetch without throwing on ok', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue(jsonResponse(200, { hi: 1 }));
    const res = await authedFetch('/raw');
    expect(res.ok).toBe(true);
    expect(await res.json()).toEqual({ hi: 1 });
  });
});
