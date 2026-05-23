import { create } from 'zustand';

export type ThemeMode = 'light' | 'dark' | 'system';

interface ThemeState {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
}

const STORAGE_KEY = 'licencebackend.theme';

function readStoredMode(): ThemeMode {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    if (value === 'light' || value === 'dark' || value === 'system') return value;
  } catch {
    // localStorage may be unavailable (SSR, sandboxed contexts); fall through to default.
  }
  return 'system';
}

function prefersDark(): boolean {
  if (typeof globalThis.window === 'undefined' || typeof globalThis.matchMedia !== 'function') return false;
  try {
    return globalThis.matchMedia('(prefers-color-scheme: dark)').matches;
  } catch {
    return false;
  }
}

function effective(mode: ThemeMode): 'light' | 'dark' {
  if (mode === 'system') return prefersDark() ? 'dark' : 'light';
  return mode;
}

function apply(mode: ThemeMode): void {
  if (typeof document === 'undefined') return;
  document.documentElement.classList.toggle('dark', effective(mode) === 'dark');
}

export const useThemeStore = create<ThemeState>((set) => ({
  mode: readStoredMode(),
  setMode: (mode) => {
    try {
      localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // ignore storage errors; the in-memory state still drives the current session.
    }
    apply(mode);
    set({ mode });
  },
}));

apply(useThemeStore.getState().mode);

if (typeof globalThis.window !== 'undefined' && typeof globalThis.matchMedia === 'function') {
  try {
    const mq = globalThis.matchMedia('(prefers-color-scheme: dark)');
    mq.addEventListener('change', () => {
      if (useThemeStore.getState().mode === 'system') apply('system');
    });
  } catch {
    // matchMedia is missing addEventListener in some old envs; ignore.
  }
}
