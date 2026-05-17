import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { NavItem } from '../components/layout/NavItem';

function renderAt(path: string) {
  const rootRoute = createRootRoute({
    component: () => (
      <nav>
        <NavItem to="/admin/licences" label="Licences" />
        <NavItem to="/admin/products" label="Products" />
      </nav>
    ),
  });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => null });
  const licencesRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/licences', component: () => null });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/admin/products', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, licencesRoute, productsRoute]),
    history: createMemoryHistory({ initialEntries: [path] }),
  });
  render(<RouterProvider router={router} />);
}

describe('NavItem', () => {
  it('renders the label as a link to the target', async () => {
    renderAt('/');
    const link = await screen.findByRole('link', { name: 'Licences' });
    expect(link).toHaveAttribute('href', '/admin/licences');
  });

  it('marks the link active when the current route matches', async () => {
    renderAt('/admin/licences');
    const link = await screen.findByRole('link', { name: 'Licences' });
    expect(link).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark other links active', async () => {
    renderAt('/admin/licences');
    const other = await screen.findByRole('link', { name: 'Products' });
    expect(other).not.toHaveAttribute('aria-current', 'page');
  });
});
