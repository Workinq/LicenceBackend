import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AddToBasketButton } from '../components/basket/AddToBasketButton';
import { useBasketStore } from '../state/basket-store';
import { useAccessTokenStore } from '../auth/access-token-store';
import type { ProductResponse } from '../api/generated/api.schemas';

function makeProduct(): ProductResponse {
  return {
    id: 'p1',
    slug: 'widget',
    displayName: 'Widget',
    imageUrl: null,
    price: 1000,
    currency: 'usd',
    description: '',
    pageContent: '',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  } as ProductResponse;
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

describe('AddToBasketButton', () => {
  it('renders the Add to basket call to action when not in basket', () => {
    render(<AddToBasketButton product={makeProduct()} />);
    expect(screen.getByRole('button', { name: /add to basket/i })).toBeInTheDocument();
  });

  it('adds the product to the basket on click', async () => {
    render(<AddToBasketButton product={makeProduct()} />);
    await userEvent.click(screen.getByRole('button', { name: /add to basket/i }));
    expect(useBasketStore.getState().items).toHaveLength(1);
    expect(useBasketStore.getState().items[0].productId).toBe('p1');
  });

  it('shows the stepper with current quantity once the product is in the basket', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().setQuantity('p1', 3);
    render(<AddToBasketButton product={makeProduct()} />);
    expect(screen.getByLabelText('Quantity 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /increase quantity/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /decrease quantity/i })).toBeInTheDocument();
  });

  it('increment button increases the quantity', async () => {
    useBasketStore.getState().add(makeProduct());
    render(<AddToBasketButton product={makeProduct()} />);
    await userEvent.click(screen.getByRole('button', { name: /increase quantity/i }));
    expect(useBasketStore.getState().items[0].quantity).toBe(2);
  });

  it('decrement removes the item when quantity is one', async () => {
    useBasketStore.getState().add(makeProduct());
    render(<AddToBasketButton product={makeProduct()} />);
    await userEvent.click(screen.getByRole('button', { name: /remove from basket/i }));
    expect(useBasketStore.getState().items).toHaveLength(0);
  });

  it('decrement decreases the quantity when greater than one', async () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().setQuantity('p1', 2);
    render(<AddToBasketButton product={makeProduct()} />);
    await userEvent.click(screen.getByRole('button', { name: /decrease quantity/i }));
    expect(useBasketStore.getState().items[0].quantity).toBe(1);
  });

  it('compact variant renders smaller controls without changing behaviour', async () => {
    const { container } = render(<AddToBasketButton product={makeProduct()} variant="compact" />);
    expect(within(container).getByRole('button', { name: /add to basket/i })).toHaveClass('h-7');
  });
});
