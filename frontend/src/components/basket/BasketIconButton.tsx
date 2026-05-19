import { Link } from '@tanstack/react-router';
import { ShoppingCart } from 'lucide-react';
import { basketCount, useBasketStore } from '@/state/basket-store';

export function BasketIconButton() {
  const items = useBasketStore((s) => s.items);
  const count = basketCount(items);
  return (
    <Link
      to="/portal/basket"
      aria-label={count > 0 ? `Basket (${count} item${count === 1 ? '' : 's'})` : 'Basket (empty)'}
      className="relative inline-flex h-9 w-9 items-center justify-center rounded-md text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink"
    >
      <ShoppingCart className="size-5" aria-hidden="true" />
      {count > 0 && (
        <span
          aria-hidden="true"
          className="absolute -right-0.5 -top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-ink px-1 text-[10px] font-semibold leading-none text-surface-elevated"
        >
          {count > 99 ? '99+' : count}
        </span>
      )}
    </Link>
  );
}
