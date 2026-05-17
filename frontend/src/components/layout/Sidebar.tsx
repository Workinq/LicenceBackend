import { LayoutDashboard, KeyRound, Package, Users } from 'lucide-react';
import { NavItem } from './NavItem';

export function Sidebar() {
  return (
    <aside className="hidden w-56 shrink-0 border-r border-border bg-surface-elevated md:flex md:flex-col">
      <nav className="flex flex-col gap-1 p-3">
        <NavItem to="/admin" label="Overview" icon={LayoutDashboard} exact />
        <NavItem to="/admin/licences" label="Licences" icon={KeyRound} />
        <NavItem to="/admin/products" label="Products" icon={Package} />
        <NavItem to="/admin/users" label="Users" icon={Users} />
      </nav>
    </aside>
  );
}
