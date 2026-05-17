import { LayoutDashboard, KeyRound, Package } from 'lucide-react';
import { NavItem } from './NavItem';

export function PortalSidebar() {
  return (
    <aside className="hidden w-56 shrink-0 border-r border-border bg-surface-elevated md:flex md:flex-col">
      <nav className="flex flex-col gap-1 p-3">
        <NavItem to="/portal" label="Overview" icon={LayoutDashboard} exact />
        <NavItem to="/portal/licences" label="Licences" icon={KeyRound} />
        <NavItem to="/portal/products" label="Products" icon={Package} />
      </nav>
    </aside>
  );
}
