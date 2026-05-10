// frontend/src/routes/_authed/index.tsx
import { createFileRoute } from '@tanstack/react-router';
import { useAccessTokenStore } from '../../auth/access-token-store';

export const Route = createFileRoute('/_authed/')({
  component: DashboardPlaceholder,
});

function DashboardPlaceholder() {
  const user = useAccessTokenStore((s) => s.user);

  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink mb-4">Dashboard</h1>
      {user ? (
        <p className="text-ink-muted">
          Logged in as <strong className="text-ink">{user.email}</strong> ({user.role})
        </p>
      ) : (
        <p className="text-ink-muted">Loading session...</p>
      )}
      <p className="mt-4 text-sm text-ink-subtle">
        P1b will replace this placeholder with the full layout and navigation.
      </p>
    </div>
  );
}
