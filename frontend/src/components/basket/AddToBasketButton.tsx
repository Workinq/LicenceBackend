import type { MouseEvent } from 'react';
import { Minus, Plus, ShoppingCart } from 'lucide-react';
import { useBasketStore } from '@/state/basket-store';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ProductResponse } from '@/api/generated/api.schemas';

interface AddToBasketButtonProps {
  product: ProductResponse;
  variant?: 'default' | 'compact';
}

export function AddToBasketButton({ product, variant = 'default' }: Readonly<AddToBasketButtonProps>) {
  const item = useBasketStore((s) => s.items.find((i) => i.productId === product.id));
  const add = useBasketStore((s) => s.add);
  const setQuantity = useBasketStore((s) => s.setQuantity);
  const remove = useBasketStore((s) => s.remove);

  if (!item) {
    return (
      <Button
        size={variant === 'compact' ? 'sm' : 'default'}
        onClick={(e) => {
          e.preventDefault();
          e.stopPropagation();
          add(product);
        }}
        className={cn('gap-1.5', variant === 'compact' && 'h-7 px-2 text-xs')}
      >
        <ShoppingCart className={cn('shrink-0', variant === 'compact' ? 'size-3.5' : 'size-4')} aria-hidden="true" />
        Add to basket
      </Button>
    );
  }

  const dec = (e: MouseEvent) => {
    e.stopPropagation();
    if (item.quantity <= 1) {
      remove(product.id);
    } else {
      setQuantity(product.id, item.quantity - 1);
    }
  };
  const inc = (e: MouseEvent) => {
    e.stopPropagation();
    setQuantity(product.id, item.quantity + 1);
  };

  return (
    <div
      className={cn(
        'inline-flex items-center rounded-md border border-border bg-surface-elevated',
        variant === 'compact' ? 'h-7' : 'h-9',
      )}
    >
      <button
        type="button"
        onClick={dec}
        aria-label={item.quantity > 1 ? 'Decrease quantity' : 'Remove from basket'}
        className={cn(
          'flex items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink',
          variant === 'compact' ? 'h-7 w-7' : 'h-9 w-9',
        )}
      >
        <Minus className={cn(variant === 'compact' ? 'size-3' : 'size-3.5')} aria-hidden="true" />
      </button>
      <span
        className={cn(
          'flex-1 text-center font-medium tabular-nums',
          variant === 'compact' ? 'min-w-7 px-1 text-xs' : 'min-w-10 px-2 text-sm',
        )}
        aria-label={`Quantity ${item.quantity}`}
      >
        {item.quantity}
      </span>
      <button
        type="button"
        onClick={inc}
        aria-label="Increase quantity"
        className={cn(
          'flex items-center justify-center text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink',
          variant === 'compact' ? 'h-7 w-7' : 'h-9 w-9',
        )}
      >
        <Plus className={cn(variant === 'compact' ? 'size-3' : 'size-3.5')} aria-hidden="true" />
      </button>
    </div>
  );
}
