import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

const STORAGE_KEY = 'licencebackend.theme';

interface FakeMediaQuery {
  matches: boolean;
  media: string;
  onchange: null;
  addListener: () => void;
  removeListener: () => void;
  addEventListener: (event: string, handler: () => void) => void;
  removeEventListener: () => void;
  dispatchEvent: () => boolean;
  __fire: () => void;
}

function installMatchMedia(matches: boolean, opts: { throwOnAdd?: boolean; throwOnMatch?: boolean } = {}) {
  let listener: (() => void) | null = null;
  const mq: FakeMediaQuery = {
    matches,
    media: '(prefers-color-scheme: dark)',
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: (_event, handler) => {
      if (opts.throwOnAdd) throw new Error('addEventListener not supported');
      listener = handler;
    },
    removeEventListener: () => {},
    dispatchEvent: () => false,
    __fire: () => listener?.(),
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn(() => {
      if (opts.throwOnMatch) throw new Error('matchMedia blew up');
      return mq;
    }),
  });
  return mq;
}

function clearMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: undefined,
  });
}

beforeEach(() => {
  window.localStorage.clear();
  document.documentElement.classList.remove('dark');
  vi.resetModules();
});

afterEach(() => {
  document.documentElement.classList.remove('dark');
});

describe('theme-store', () => {
  it('defaults to system when no stored value is present', async () => {
    installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    expect(useThemeStore.getState().mode).toBe('system');
  });

  it('reads a stored mode from localStorage on initialisation', async () => {
    window.localStorage.setItem(STORAGE_KEY, 'dark');
    installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    expect(useThemeStore.getState().mode).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('falls back to system when the stored value is not a known mode', async () => {
    window.localStorage.setItem(STORAGE_KEY, 'rainbow');
    installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    expect(useThemeStore.getState().mode).toBe('system');
  });

  it('setMode persists to localStorage and toggles the dark class', async () => {
    installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('dark');
    expect(useThemeStore.getState().mode).toBe('dark');
    expect(window.localStorage.getItem(STORAGE_KEY)).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);

    useThemeStore.getState().setMode('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('applies dark when the system prefers dark and mode is system', async () => {
    installMatchMedia(true);
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('system');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('applies light when the system does not prefer dark and mode is system', async () => {
    installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('system');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('survives a missing matchMedia by treating system as light', async () => {
    clearMatchMedia();
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('system');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('survives a matchMedia that throws by treating system as light', async () => {
    installMatchMedia(false, { throwOnMatch: true });
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('system');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('reapplies the theme when the system colour scheme changes while in system mode', async () => {
    const mq = installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('system');
    mq.matches = true;
    mq.__fire();
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('ignores system colour scheme changes when the mode is fixed', async () => {
    const mq = installMatchMedia(false);
    const { useThemeStore } = await import('../theme/theme-store');
    useThemeStore.getState().setMode('light');
    mq.matches = true;
    mq.__fire();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('survives a matchMedia missing addEventListener at module init', async () => {
    installMatchMedia(false, { throwOnAdd: true });
    const { useThemeStore } = await import('../theme/theme-store');
    expect(useThemeStore.getState().mode).toBe('system');
  });
});
