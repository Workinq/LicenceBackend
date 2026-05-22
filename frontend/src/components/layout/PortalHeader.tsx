import { AccountMenu } from '@/components/layout/AccountMenu';
import { BasketIconButton } from '@/components/basket/BasketIconButton';

export function PortalHeader() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-surface-elevated px-4 md:px-6">
      <span className="font-display text-lg font-semibold text-ink">My account</span>

      <div className="flex items-center gap-2">
        <BasketIconButton />
        <AccountMenu profileHref="/portal/me" />
      </div>
    </header>
  );
}
