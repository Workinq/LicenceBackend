// frontend/src/routes/_authed/index.tsx
import { createFileRoute } from '@tanstack/react-router';
import { useAccessTokenStore } from '../../auth/access-token-store';

export const Route = createFileRoute('/_authed/')({
  component: OverviewPage,
});

function OverviewPage() {
  const user = useAccessTokenStore((s) => s.user);

  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink">Overview</h1>
      {user ? (
        <p className="mt-2 text-ink-muted">
          Signed in as <strong className="text-ink">{user.email}</strong> ({user.role}).
        </p>
      ) : (
        <p className="mt-2 text-ink-muted">Loading session...</p>
      )}
    </div>
  );
}
