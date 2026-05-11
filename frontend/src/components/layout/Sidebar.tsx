import { LayoutDashboard, KeyRound, Package, Users } from 'lucide-react';
import { NavItem } from './NavItem';

export function Sidebar() {
  return (
    <aside className="hidden w-56 shrink-0 border-r border-border bg-surface-elevated md:flex md:flex-col">
      <nav className="flex flex-col gap-1 p-3">
        <NavItem to="/" label="Overview" icon={LayoutDashboard} />
        <NavItem to="/licences" label="Licences" icon={KeyRound} />
        <NavItem to="/products" label="Products" icon={Package} />
        <NavItem to="/users" label="Users" icon={Users} />
      </nav>
    </aside>
  );
}
