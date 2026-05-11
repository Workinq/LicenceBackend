// frontend/src/routes/_authed/me.tsx
import { createFileRoute } from '@tanstack/react-router';
import { useAccessTokenStore } from '../../auth/access-token-store';

export const Route = createFileRoute('/_authed/me')({
  component: MePlaceholder,
});

function MePlaceholder() {
  const user = useAccessTokenStore((s) => s.user);

  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink mb-4">My Profile</h1>
      {user ? (
        <dl className="text-sm space-y-2">
          <div>
            <dt className="text-ink-muted inline">Email: </dt>
            <dd className="text-ink inline">{user.email}</dd>
          </div>
          <div>
            <dt className="text-ink-muted inline">Role: </dt>
            <dd className="text-ink inline">{user.role}</dd>
          </div>
          <div>
            <dt className="text-ink-muted inline">Status: </dt>
            <dd className="text-ink inline">{user.status}</dd>
          </div>
        </dl>
      ) : (
        <p className="text-ink-muted">Loading...</p>
      )}
    </div>
  );
}
