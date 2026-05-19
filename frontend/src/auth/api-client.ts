// frontend/src/auth/api-client.ts
import { useAccessTokenStore, type AuthUser } from './access-token-store';

// All backend calls go through this prefix; the dev proxy strips it. See vite.config.ts.
export const API_BASE = '/api';

// Single-flight refresh guard - ensures concurrent 401s fire only one refresh request.
let refreshPromise: Promise<boolean> | null = null;

// Fetch mutator used by orval-generated api.ts.
// The fetch client codegen expects: (url, init?) => Promise<{ data: unknown, status: number, headers: Headers }>
export const apiClient = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const response = await authedFetch(url, init);

  if (!response.ok) {
    throw new ApiError(response.status, await safeJson(response));
  }

  const data: unknown = response.status === 204 ? undefined : (await response.json() as unknown);
  return { data, status: response.status, headers: response.headers } as T;
};

// Shared bearer + refresh flow. Returns the raw Response so callers can read it as JSON, blob, stream, etc.
export const authedFetch = async (url: string, init?: RequestInit): Promise<Response> => {
  const send = async (): Promise<Response> => {
    const accessToken = useAccessTokenStore.getState().accessToken;
    return fetch(`${API_BASE}${url}`, {
      credentials: 'include',
      ...init,
      headers: {
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        ...init?.headers,
      },
    });
  };

  let response = await send();

  if (response.status === 401 && !url.endsWith('/sessions/refresh')) {
    const refreshed = await singleFlightRefresh();
    if (refreshed) {
      response = await send();
    } else {
      useAccessTokenStore.getState().clear();
      window.location.assign('/login');
      throw new ApiError(401, null);
    }
  }

  return response;
};

interface RefreshBody {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
}

const singleFlightRefresh = async (): Promise<boolean> => {
  if (refreshPromise) return refreshPromise;

  refreshPromise = (async () => {
    try {
      const res = await fetch(`${API_BASE}/sessions/refresh`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!res.ok) return false;
      const body = (await res.json()) as RefreshBody;
      useAccessTokenStore
        .getState()
        .setSession(body.accessToken, new Date(body.accessTokenExpiresAt), body.user);
      return true;
    } catch {
      return false;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
};

const safeJson = async (r: Response): Promise<unknown> => {
  try {
    return await r.json();
  } catch {
    return null;
  }
};

export class ApiError extends Error {
  status: number;
  body: unknown;

  constructor(status: number, body: unknown) {
    super(`API error ${status}`);
    this.status = status;
    this.body = body;
  }
}
