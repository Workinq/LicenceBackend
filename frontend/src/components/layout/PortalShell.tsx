import { Outlet } from '@tanstack/react-router';
import { PortalHeader } from './PortalHeader';
import { PortalSidebar } from './PortalSidebar';

export function PortalShell() {
  return (
    <div className="flex h-screen flex-col bg-surface text-ink">
      <PortalHeader />
      <div className="flex min-h-0 flex-1">
        <PortalSidebar />
        <main className="min-w-0 flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
