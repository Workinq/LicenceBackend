// frontend/src/auth/api-client.ts
import { useAccessTokenStore, type AuthUser } from './access-token-store';

// Single-flight refresh guard  - ensures concurrent 401s fire only one refresh request.
let refreshPromise: Promise<boolean> | null = null;

// Fetch mutator used by orval-generated api.ts  - signature: (url, init?) => Promise<T>
export const apiClient = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const send = async (): Promise<Response> => {
    const accessToken = useAccessTokenStore.getState().accessToken;
    return fetch(url, {
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

  if (!response.ok) {
    throw new ApiError(response.status, await safeJson(response));
  }

  return (response.status === 204 ? (undefined as T) : await response.json()) as T;
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
      const res = await fetch('/sessions/refresh', {
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
