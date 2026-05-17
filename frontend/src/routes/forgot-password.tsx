import { createFileRoute, Link } from '@tanstack/react-router';
import { buttonVariants } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

export const Route = createFileRoute('/forgot-password')({
  component: ForgotPasswordPage,
});

function ForgotPasswordPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface px-4">
      <div className="w-full max-w-md space-y-6">
        <h1 className="font-display text-3xl font-semibold text-ink text-center">
          Forgot password
        </h1>

        <Alert>
          <AlertTitle>Self-service reset is not available yet.</AlertTitle>
          <AlertDescription>
            Please contact an administrator and ask them to reset your password. They will share a
            new temporary password with you out of band.
          </AlertDescription>
        </Alert>

        <div className="flex justify-center">
          <Link to="/login" className={buttonVariants({ variant: 'outline' })}>
            Back to sign in
          </Link>
        </div>
      </div>
    </div>
  );
}
