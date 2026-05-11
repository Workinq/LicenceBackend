import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { NotFound } from '../components/layout/NotFound';

describe('NotFound', () => {
  it('renders a not-found message and a link home', async () => {
    const rootRoute = createRootRoute();
    const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => <div>home</div> });
    const router = createRouter({
      routeTree: rootRoute.addChildren([indexRoute]),
      history: createMemoryHistory({ initialEntries: ['/does-not-exist'] }),
      defaultNotFoundComponent: NotFound,
    });
    render(<RouterProvider router={router} />);
    expect(await screen.findByText(/page not found/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /go to overview/i })).toHaveAttribute('href', '/');
  });
});
