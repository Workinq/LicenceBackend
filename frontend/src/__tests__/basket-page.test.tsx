import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { Route as BasketRoute } from '../routes/portal/basket';
import { useBasketStore } from '../state/basket-store';
import { useAccessTokenStore } from '../auth/access-token-store';
import type { BasketItem } from '../state/basket-store';

function makeItem(overrides: Partial<BasketItem> = {}): BasketItem {
  return {
    productId: 'p1',
    slug: 'widget',
    displayName: 'Widget',
    imageUrl: null,
    unitPrice: 1000,
    currency: 'USD',
    quantity: 1,
    labels: [null],
    ...overrides,
  };
}

function renderBasket() {
  const rootRoute = createRootRoute();
  const basketRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/basket', component: BasketRoute.options.component });
  const productsRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/products', component: () => null });
  const checkoutRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/checkout', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([basketRoute, productsRoute, checkoutRoute]),
    history: createMemoryHistory({ initialEntries: ['/portal/basket'] }),
  });
  render(<RouterProvider router={router} />);
}

beforeEach(() => {
  window.localStorage.clear();
  useBasketStore.setState({ items: [] });
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id: 'u1',
    email: 'u1@example.com',
    displayName: 'u1',
    role: 'user',
    status: 'active',
    createdAt: new Date().toISOString(),
  });
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
  window.localStorage.clear();
});

describe('BasketPage', () => {
  it('shows the empty state with a browse link when no items are in the basket', async () => {
    renderBasket();
    expect(await screen.findByText(/your basket is empty/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /browse the catalog/i })).toBeInTheDocument();
  });

  it('renders a row per item with quantity, line totals, and order total', async () => {
    useBasketStore.setState({
      items: [
        makeItem({ productId: 'a', displayName: 'Alpha', unitPrice: 1000, quantity: 2 }),
        makeItem({ productId: 'b', displayName: 'Beta', unitPrice: 500, quantity: 3, currency: 'EUR' }),
      ],
    });
    renderBasket();
    expect(await screen.findByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
    expect(screen.getByText(/5 units ready to check out/i)).toBeInTheDocument();
  });

  it('clicking remove drops the row from the store', async () => {
    useBasketStore.setState({ items: [makeItem()] });
    renderBasket();
    await userEvent.click(await screen.findByRole('button', { name: /remove from basket/i }));
    expect(useBasketStore.getState().items).toHaveLength(0);
  });

  it('clicking increase raises the quantity in the store', async () => {
    useBasketStore.setState({ items: [makeItem({ quantity: 1 })] });
    renderBasket();
    await userEvent.click(await screen.findByRole('button', { name: /increase quantity/i }));
    expect(useBasketStore.getState().items[0].quantity).toBe(2);
  });

  it('decrease is disabled when quantity is one', async () => {
    useBasketStore.setState({ items: [makeItem({ quantity: 1 })] });
    renderBasket();
    expect(await screen.findByRole('button', { name: /decrease quantity/i })).toBeDisabled();
  });

  it('typing additional digits in the input updates the store', async () => {
    useBasketStore.setState({ items: [makeItem({ quantity: 2 })] });
    renderBasket();
    const input = await screen.findByDisplayValue('2');
    await userEvent.type(input, '5');
    expect(useBasketStore.getState().items[0].quantity).toBe(25);
  });

  it('shows free pricing when the unit price is null', async () => {
    useBasketStore.setState({
      items: [makeItem({ unitPrice: null, quantity: 2 })],
    });
    renderBasket();
    expect(await screen.findAllByText('Free')).not.toHaveLength(0);
  });
});
