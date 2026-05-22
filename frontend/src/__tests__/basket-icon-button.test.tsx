import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  createRootRoute,
  createRoute,
  createRouter,
  createMemoryHistory,
  RouterProvider,
} from '@tanstack/react-router';
import { BasketIconButton } from '../components/basket/BasketIconButton';
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
    currency: 'usd',
    quantity: 1,
    labels: [null],
    ...overrides,
  };
}

function renderIcon() {
  const rootRoute = createRootRoute({ component: () => <BasketIconButton /> });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: () => null });
  const basketRoute = createRoute({ getParentRoute: () => rootRoute, path: '/portal/basket', component: () => null });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute, basketRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
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

describe('BasketIconButton', () => {
  it('labels the link as empty when no items are in the basket', async () => {
    renderIcon();
    expect(await screen.findByLabelText(/basket \(empty\)/i)).toBeInTheDocument();
  });

  it('shows a count badge with singular wording for one item', async () => {
    useBasketStore.setState({ items: [makeItem({ quantity: 1 })] });
    renderIcon();
    expect(await screen.findByLabelText(/basket \(1 item\)/i)).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('uses plural wording and sums quantity across items', async () => {
    useBasketStore.setState({
      items: [makeItem({ productId: 'a', quantity: 2 }), makeItem({ productId: 'b', quantity: 3 })],
    });
    renderIcon();
    expect(await screen.findByLabelText(/basket \(5 items\)/i)).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('caps the badge text at 99+ for very large baskets', async () => {
    useBasketStore.setState({ items: [makeItem({ quantity: 150 })] });
    renderIcon();
    expect(await screen.findByText('99+')).toBeInTheDocument();
  });
});
