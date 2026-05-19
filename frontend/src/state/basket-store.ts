import { create } from 'zustand';
import { useAccessTokenStore } from '@/auth/access-token-store';
import type { ProductResponse } from '@/api/generated/api.schemas';

export interface BasketItem {
  productId: string;
  slug: string;
  displayName: string;
  imageUrl: string | null;
  unitPrice: number | null;
  currency: string;
  quantity: number;
  labels: (string | null)[];
}

interface BasketState {
  items: BasketItem[];
  add: (product: ProductResponse) => void;
  remove: (productId: string) => void;
  setQuantity: (productId: string, qty: number) => void;
  setLabel: (productId: string, index: number, label: string | null) => void;
  clear: () => void;
}

const STORAGE_PREFIX = 'basket:';

function storageKey(userId: string): string {
  return STORAGE_PREFIX + userId;
}

function loadItems(userId: string | null): BasketItem[] {
  if (!userId || typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(storageKey(userId));
    return raw ? (JSON.parse(raw) as BasketItem[]) : [];
  } catch {
    return [];
  }
}

function saveItems(userId: string | null, items: BasketItem[]): void {
  if (!userId || typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(storageKey(userId), JSON.stringify(items));
  } catch {
    // ignore quota / privacy errors
  }
}

function padLabels(labels: (string | null)[], length: number): (string | null)[] {
  if (labels.length === length) return labels;
  if (labels.length > length) return labels.slice(0, length);
  const padded = [...labels];
  while (padded.length < length) padded.push(null);
  return padded;
}

const currentUserId = (): string | null => useAccessTokenStore.getState().user?.id ?? null;

export const useBasketStore = create<BasketState>((set, get) => ({
  items: loadItems(currentUserId()),
  add: (product) => {
    const uid = currentUserId();
    const items = get().items;
    const idx = items.findIndex((i) => i.productId === product.id);
    let next: BasketItem[];
    if (idx === -1) {
      next = [
        ...items,
        {
          productId: product.id,
          slug: product.slug,
          displayName: product.displayName,
          imageUrl: product.imageUrl,
          unitPrice: product.price,
          currency: product.currency,
          quantity: 1,
          labels: [null],
        },
      ];
    } else {
      next = items.map((item, i) => {
        if (i !== idx) return item;
        const quantity = item.quantity + 1;
        return { ...item, quantity, labels: padLabels(item.labels, quantity) };
      });
    }
    set({ items: next });
    saveItems(uid, next);
  },
  remove: (productId) => {
    const uid = currentUserId();
    const next = get().items.filter((i) => i.productId !== productId);
    set({ items: next });
    saveItems(uid, next);
  },
  setQuantity: (productId, qty) => {
    const uid = currentUserId();
    const safe = Math.max(1, Math.floor(qty));
    const next = get().items.map((item) =>
      item.productId === productId
        ? { ...item, quantity: safe, labels: padLabels(item.labels, safe) }
        : item,
    );
    set({ items: next });
    saveItems(uid, next);
  },
  setLabel: (productId, index, label) => {
    const uid = currentUserId();
    const next = get().items.map((item) => {
      if (item.productId !== productId) return item;
      const labels = [...item.labels];
      labels[index] = label;
      return { ...item, labels };
    });
    set({ items: next });
    saveItems(uid, next);
  },
  clear: () => {
    const uid = currentUserId();
    set({ items: [] });
    saveItems(uid, []);
  },
}));

useAccessTokenStore.subscribe((state, prev) => {
  if (state.user?.id !== prev.user?.id) {
    useBasketStore.setState({ items: loadItems(state.user?.id ?? null) });
  }
});

export function basketCount(items: BasketItem[]): number {
  return items.reduce((sum, i) => sum + i.quantity, 0);
}

export function basketTotalsByCurrency(items: BasketItem[]): { currency: string; amount: number }[] {
  const map = new Map<string, number>();
  for (const item of items) {
    const price = item.unitPrice ?? 0;
    map.set(item.currency, (map.get(item.currency) ?? 0) + price * item.quantity);
  }
  return Array.from(map.entries())
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([currency, amount]) => ({ currency, amount }));
}
