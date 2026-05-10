// frontend/src/routes/login.tsx
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/login')({
  component: LoginPage,
});

function LoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface">
      <p className="text-ink-muted font-sans">Login form — coming in Task 9</p>
    </div>
  );
}
