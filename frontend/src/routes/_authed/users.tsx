import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/users')({
  component: UsersPage,
});

function UsersPage() {
  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink">Users</h1>
      <p className="mt-2 text-sm text-ink-subtle">Coming in Chunk P1e.</p>
    </div>
  );
}
