import { LayoutDashboard, KeyRound, Package, Receipt, Users } from 'lucide-react';
import { NavItem } from './NavItem';
import { SidebarStatusFooter } from './SidebarStatusFooter';
import { useAdminSidebarCounts } from './use-sidebar-counts';

const formatCount = (n: number | undefined): string | undefined => {
  if (n === undefined) return undefined;
  if (n >= 1000) return `${(n / 1000).toFixed(1)}k`;
  return String(n);
};

export function Sidebar() {
  const counts = useAdminSidebarCounts();

  return (
    <aside className="hidden w-[220px] shrink-0 flex-col border-r border-border bg-surface-elevated md:flex">
      <nav className="flex flex-col gap-0.5 p-3">
        <SectionLabel>Workspace</SectionLabel>
        <NavItem to="/admin" label="Overview" icon={LayoutDashboard} exact />
        <NavItem to="/admin/licences" label="Licences" icon={KeyRound} badge={formatCount(counts.licences)} />
        <NavItem to="/admin/products" label="Products" icon={Package} badge={formatCount(counts.products)} />

        <SectionLabel className="mt-4">Operations</SectionLabel>
        <NavItem to="/admin/orders" label="Orders" icon={Receipt} badge={formatCount(counts.orders)} />
        <NavItem to="/admin/users" label="Users" icon={Users} badge={formatCount(counts.users)} />
      </nav>
      <SidebarStatusFooter />
    </aside>
  );
}

function SectionLabel({ children, className }: Readonly<{ children: React.ReactNode; className?: string }>) {
  return (
    <div
      className={`px-2.5 pb-1.5 pt-1 text-[10px] font-semibold uppercase tracking-wider text-ink-subtle ${className ?? ''}`}
    >
      {children}
    </div>
  );
}
