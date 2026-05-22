import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { Route as ForgotPasswordRoute } from '../routes/forgot-password';

function renderForgotPassword() {
  const rootRoute = createRootRoute();
  const forgotRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/forgot-password',
    component: ForgotPasswordRoute.options.component,
  });
  const loginRoute = createRoute({ getParentRoute: () => rootRoute, path: '/login', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([forgotRoute, loginRoute]),
    history: createMemoryHistory({ initialEntries: ['/forgot-password'] }),
  });
  render(<RouterProvider router={router} />);
}

describe('ForgotPasswordPage', () => {
  it('renders the forgot password heading and unavailable notice', async () => {
    renderForgotPassword();
    expect(await screen.findByRole('heading', { name: /forgot password/i })).toBeInTheDocument();
    expect(screen.getByText(/self-service reset is not available yet/i)).toBeInTheDocument();
  });

  it('renders a link back to sign in', async () => {
    renderForgotPassword();
    const link = await screen.findByRole('link', { name: /back to sign in/i });
    expect(link).toHaveAttribute('href', '/login');
  });
});
