// frontend/src/routes/login.tsx
import { useEffect, useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { Eye, EyeOff } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { useAccessTokenStore, type AuthUser } from '../auth/access-token-store';
import { API_BASE } from '../auth/api-client';

const searchSchema = z.object({
  reason: z.enum(['admin_required']).optional(),
});

export const Route = createFileRoute('/login')({
  component: LoginPage,
  validateSearch: searchSchema,
});

const schema = z.object({
  email: z.string().email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
});

type FormValues = z.infer<typeof schema>;

interface SessionResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: AuthUser;
}

function LoginPage() {
  const navigate = useNavigate();
  const setSession = useAccessTokenStore((s) => s.setSession);
  const { reason } = Route.useSearch();
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    if (reason === 'admin_required') {
      toast.error('This dashboard is admin only. Sign in with an admin account.');
    }
  }, [reason]);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const onSubmit = async (values: FormValues) => {
    try {
      const res = await fetch(`${API_BASE}/sessions`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
      });

      if (res.status === 401) {
        toast.error('Invalid email or password');
        return;
      }

      if (!res.ok) {
        toast.error('Login failed - please try again');
        return;
      }

      const body = (await res.json()) as SessionResponse;
      setSession(body.accessToken, new Date(body.accessTokenExpiresAt), body.user);
      await navigate({ to: body.user.role === 'admin' ? '/admin' : '/portal' });
    } catch {
      toast.error('Network error - is the API running?');
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-surface px-4">
      <div className="w-full max-w-sm">
        <h1 className="font-display text-3xl font-semibold text-ink mb-8 text-center">
          Sign in
        </h1>

        <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
          <div>
            <label htmlFor="email" className="block text-sm font-medium text-ink mb-1">
              Email
            </label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              {...register('email')}
              className="w-full rounded border border-border bg-surface-elevated px-3 py-2 text-sm text-ink placeholder:text-ink-subtle focus:outline-none focus:ring-2 focus:ring-accent focus:border-accent"
              placeholder="admin@example.com"
            />
            {errors.email && (
              <p className="mt-1 text-xs text-status-revoked-fg">{errors.email.message}</p>
            )}
          </div>

          <div>
            <div className="flex items-baseline justify-between mb-1">
              <label htmlFor="password" className="block text-sm font-medium text-ink">
                Password
              </label>
              <Link
                to="/forgot-password"
                className="text-xs text-ink-muted underline-offset-2 hover:text-ink hover:underline"
              >
                Forgot password?
              </Link>
            </div>
            <div className="relative">
              <input
                id="password"
                type={showPassword ? 'text' : 'password'}
                autoComplete="current-password"
                {...register('password')}
                className="w-full rounded border border-border bg-surface-elevated px-3 py-2 pr-10 text-sm text-ink placeholder:text-ink-subtle focus:outline-none focus:ring-2 focus:ring-accent focus:border-accent"
                placeholder="********"
              />
              <button
                type="button"
                onClick={() => { setShowPassword((v) => !v); }}
                aria-label={showPassword ? 'Hide password' : 'Show password'}
                aria-pressed={showPassword}
                className="absolute inset-y-0 right-0 flex items-center px-3 text-ink-muted hover:text-ink"
              >
                {showPassword ? <EyeOff className="size-4" aria-hidden="true" /> : <Eye className="size-4" aria-hidden="true" />}
              </button>
            </div>
            {errors.password && (
              <p className="mt-1 text-xs text-status-revoked-fg">{errors.password.message}</p>
            )}
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full rounded bg-ink px-4 py-2.5 text-sm font-medium text-surface-elevated hover:opacity-90 disabled:opacity-50"
          >
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </div>
    </div>
  );
}
