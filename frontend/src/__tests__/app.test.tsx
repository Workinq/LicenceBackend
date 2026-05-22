import { describe, it, expect, vi, beforeAll } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRouter,
  createMemoryHistory,
} from '@tanstack/react-router';

vi.mock('../router', () => {
  const rootRoute = createRootRoute({ component: () => <div>app stub home</div> });
  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
  });
  return { router };
});

vi.mock('@/components/ui/sonner', () => ({ Toaster: () => null }));

beforeAll(() => {
  if (!window.matchMedia) {
    Object.defineProperty(window, 'matchMedia', {
      configurable: true,
      writable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => {},
        removeListener: () => {},
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => false,
      }),
    });
  }
});

import { App } from '../App';

describe('App', () => {
  it('renders the configured router', async () => {
    render(<App />);
    expect(await screen.findByText('app stub home')).toBeInTheDocument();
  });
});
