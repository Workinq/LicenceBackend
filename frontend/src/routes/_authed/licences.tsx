import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/licences')({
  component: LicencesPage,
});

function LicencesPage() {
  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink">Licences</h1>
      <p className="mt-2 text-sm text-ink-subtle">Coming in Chunk P1c.</p>
    </div>
  );
}
