import { AccountMenu } from '@/components/layout/AccountMenu';

export function Header() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b border-border bg-surface-elevated px-4 md:px-6">
      <span className="font-display text-lg font-semibold text-ink">LicenceBackend</span>
      <AccountMenu profileHref="/admin/me" />
    </header>
  );
}
