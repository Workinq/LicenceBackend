import '@testing-library/jest-dom';

class ResizeObserverMock {
  observe() { return undefined; }
  unobserve() { return undefined; }
  disconnect() { return undefined; }
}
// eslint-disable-next-line @typescript-eslint/no-unnecessary-type-assertion
globalThis.ResizeObserver = globalThis.ResizeObserver ?? (ResizeObserverMock as typeof ResizeObserver);
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {};
}

if (typeof globalThis.window !== 'undefined' && typeof globalThis.window.localStorage?.clear !== 'function') {
  const store = new Map<string, string>();
  Object.defineProperty(globalThis.window, 'localStorage', {
    configurable: true,
    value: {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => { store.set(k, String(v)); },
      removeItem: (k: string) => { store.delete(k); },
      clear: () => { store.clear(); },
      key: (i: number) => Array.from(store.keys())[i] ?? null,
      get length() { return store.size; },
    },
  });
}
