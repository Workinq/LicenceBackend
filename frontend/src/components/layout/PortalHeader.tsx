import { KeyRound } from 'lucide-react';
import { AccountMenu } from '@/components/layout/AccountMenu';
import { BasketIconButton } from '@/components/basket/BasketIconButton';

export function PortalHeader() {
  return (
    <header className="flex h-12 shrink-0 items-center justify-between border-b border-border bg-surface-elevated px-4 md:px-6">
      <div className="flex items-center gap-2.5">
        <span className="flex size-[22px] items-center justify-center rounded-md bg-foreground text-background">
          <KeyRound className="size-3.5" strokeWidth={2.25} />
        </span>
        <span className="text-[15px] font-semibold tracking-tight text-foreground">LicenceBackend</span>
        <span className="rounded border border-border px-1.5 py-0.5 font-mono text-[11px] leading-none text-ink-muted">
          portal
        </span>
      </div>

      <div className="flex items-center gap-2">
        <BasketIconButton />
        <AccountMenu profileHref="/portal/me" />
      </div>
    </header>
  );
}
