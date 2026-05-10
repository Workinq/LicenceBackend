// frontend/src/auth/access-token-store.ts
import { create } from 'zustand';

export interface AuthUser {
  id: string;
  email: string;
  displayName: string | null;
  role: 'admin' | 'user';
  status: 'active' | 'suspended';
  createdAt: string;
}

interface AccessTokenState {
  accessToken: string | null;
  expiresAt: number | null;
  user: AuthUser | null;
  setSession: (accessToken: string, expiresAt: Date, user: AuthUser) => void;
  clear: () => void;
}

export const useAccessTokenStore = create<AccessTokenState>((set) => ({
  accessToken: null,
  expiresAt: null,
  user: null,
  setSession: (accessToken, expiresAt, user) =>
    set({ accessToken, expiresAt: expiresAt.getTime(), user }),
  clear: () => set({ accessToken: null, expiresAt: null, user: null }),
}));
