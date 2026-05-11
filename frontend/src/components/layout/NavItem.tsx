import { Link } from '@tanstack/react-router';
import type { LinkProps } from '@tanstack/react-router';
import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItemProps {
  to: LinkProps['to'];
  label: string;
  icon?: LucideIcon;
}

export function NavItem({ to, label, icon: Icon }: NavItemProps) {
  return (
    <Link
      to={to}
      aria-current={undefined}
      activeProps={{ 'aria-current': 'page' as const }}
      activeOptions={{ exact: to === '/' }}
      className={cn(
        'flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium text-ink-muted transition-colors',
        'hover:bg-surface-sunken hover:text-ink',
        'aria-[current=page]:bg-accent-soft aria-[current=page]:text-ink',
      )}
    >
      {Icon && <Icon className="size-4 shrink-0" aria-hidden="true" />}
      <span>{label}</span>
    </Link>
  );
}

export type { NavItemProps };
