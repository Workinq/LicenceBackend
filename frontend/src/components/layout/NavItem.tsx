import { Link } from '@tanstack/react-router';
import type { LinkProps } from '@tanstack/react-router';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItemProps {
  to: LinkProps['to'];
  label: string;
  icon?: LucideIcon;
  exact?: boolean;
  badge?: string | number;
}

export function NavItem({ to, label, icon: Icon, exact, badge }: NavItemProps) {
  return (
    <Link
      to={to}
      aria-current={undefined}
      activeProps={{ 'aria-current': 'page' as const }}
      activeOptions={{ exact: exact ?? to === '/' }}
      className={cn(
        'flex items-center gap-2.5 rounded-md px-2.5 py-1.5 text-[13px] font-medium text-ink-muted transition-colors',
        'hover:bg-surface-sunken hover:text-foreground',
        'aria-[current=page]:bg-accent-soft aria-[current=page]:text-foreground',
      )}
    >
      {Icon && <Icon className="size-4 shrink-0" aria-hidden="true" />}
      <span className="flex-1">{label}</span>
      {badge !== undefined && (
        <span className="font-mono text-[11px] tabular-nums text-ink-subtle">{badge}</span>
      )}
    </Link>
  );
}

export type { NavItemProps };
