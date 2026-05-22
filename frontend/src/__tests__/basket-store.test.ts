import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { useBasketStore, basketCount, basketTotalsByCurrency } from '../state/basket-store';
import { useAccessTokenStore } from '../auth/access-token-store';
import type { ProductResponse } from '../api/generated/api.schemas';

function makeProduct(overrides: Partial<ProductResponse> = {}): ProductResponse {
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
    ...overrides,
  } as ProductResponse;
}

function loginAs(id: string) {
  useAccessTokenStore.getState().setSession('tok', new Date(Date.now() + 900_000), {
    id,
    email: `${id}@example.com`,
    displayName: id,
    role: 'user',
    status: 'active',
    createdAt: new Date().toISOString(),
  });
}

beforeEach(() => {
  window.localStorage.clear();
  useBasketStore.setState({ items: [] });
  loginAs('u1');
});

afterEach(() => {
  useAccessTokenStore.getState().clear();
  window.localStorage.clear();
});

describe('basket-store', () => {
  it('add inserts a new item with quantity 1 and pads labels', () => {
    useBasketStore.getState().add(makeProduct());
    const [item] = useBasketStore.getState().items;
    expect(item.productId).toBe('p1');
    expect(item.quantity).toBe(1);
    expect(item.labels).toEqual([null]);
  });

  it('add on an existing product increments quantity and pads labels', () => {
    const product = makeProduct();
    useBasketStore.getState().add(product);
    useBasketStore.getState().add(product);
    const [item] = useBasketStore.getState().items;
    expect(item.quantity).toBe(2);
    expect(item.labels).toEqual([null, null]);
  });

  it('setQuantity clamps to at least 1 and pads/truncates labels', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().setQuantity('p1', 3);
    expect(useBasketStore.getState().items[0].labels).toHaveLength(3);
    useBasketStore.getState().setQuantity('p1', 0);
    expect(useBasketStore.getState().items[0].quantity).toBe(1);
    expect(useBasketStore.getState().items[0].labels).toHaveLength(1);
  });

  it('setQuantity floors fractional values', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().setQuantity('p1', 2.9);
    expect(useBasketStore.getState().items[0].quantity).toBe(2);
  });

  it('remove drops the matching product', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().add(makeProduct({ id: 'p2', slug: 'other', displayName: 'Other' }));
    useBasketStore.getState().remove('p1');
    expect(useBasketStore.getState().items.map((i) => i.productId)).toEqual(['p2']);
  });

  it('setLabel updates a single seat label', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().setQuantity('p1', 2);
    useBasketStore.getState().setLabel('p1', 1, 'second seat');
    expect(useBasketStore.getState().items[0].labels).toEqual([null, 'second seat']);
  });

  it('clear empties the basket and persists empty for the current user', () => {
    useBasketStore.getState().add(makeProduct());
    useBasketStore.getState().clear();
    expect(useBasketStore.getState().items).toEqual([]);
    expect(window.localStorage.getItem('basket:u1')).toBe('[]');
  });

  it('persists items per user in localStorage', () => {
    useBasketStore.getState().add(makeProduct());
    expect(window.localStorage.getItem('basket:u1')).toContain('"productId":"p1"');
  });

  it('switches baskets when the signed-in user changes', () => {
    useBasketStore.getState().add(makeProduct({ id: 'a' }));
    loginAs('u2');
    expect(useBasketStore.getState().items).toEqual([]);
    useBasketStore.getState().add(makeProduct({ id: 'b' }));
    loginAs('u1');
    expect(useBasketStore.getState().items.map((i) => i.productId)).toEqual(['a']);
  });

  it('basketCount sums quantities', () => {
    expect(basketCount([])).toBe(0);
    expect(basketCount([{ quantity: 2 }, { quantity: 3 }] as never)).toBe(5);
  });

  it('basketTotalsByCurrency groups by currency with stable order', () => {
    const items = [
      { unitPrice: 1000, currency: 'usd', quantity: 2 },
      { unitPrice: 500, currency: 'eur', quantity: 1 },
      { unitPrice: null, currency: 'usd', quantity: 1 },
    ] as never;
    expect(basketTotalsByCurrency(items)).toEqual([
      { currency: 'eur', amount: 500 },
      { currency: 'usd', amount: 2000 },
    ]);
  });
});
