import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import { createUser } from '@/api/users';
import { generatePassword } from '@/lib/generate-password';
import { ApiError } from '@/auth/api-client';
import type { UserResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/admin/users_/new')({
  component: NewUserPage,
});

const schema = z.object({
  email: z.string().min(1, 'Required').email('Enter a valid email'),
  displayName: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

interface Created {
  user: UserResponse;
  password: string;
}

function NewUserPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [created, setCreated] = useState<Created | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', displayName: '' },
  });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const password = generatePassword(24);
      const user = await createUser({
        email: values.email,
        password,
        displayName: values.displayName ? values.displayName : null,
      });
      return { user, password };
    },
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      setCreated(result);
      setSubmitError(null);
    },
    onError: (error) => {
      setSubmitError(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not create the user.',
      );
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSubmitError(null);
    await mutation.mutateAsync(values);
  };

  if (created) {
    return (
      <div className="max-w-2xl space-y-6">
        <h1 className="font-display text-2xl font-semibold text-ink">User created</h1>
        <p className="text-sm text-ink-muted">
          Share this password with {created.user.email}. It will not be shown again.
        </p>
        <SecretRevealOnce label="Initial password" value={created.password} />
        <div className="flex gap-3">
          <Button
            type="button"
            onClick={() => { void navigate({ to: '/admin/users/$id', params: { id: created.user.id } }); }}
          >
            Open user
          </Button>
          <Button
            type="button"
            variant="outline"
            onClick={() => { void navigate({ to: '/admin/users' }); }}
          >
            Back to users
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">New user</h1>
      <p className="text-sm text-ink-muted">
        A random password is generated and shown once after creation.
      </p>

      <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
        {submitError && (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-1">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" autoComplete="off" placeholder="user@example.com" {...register('email')} />
          {errors.email && <p className="text-xs text-status-revoked-fg">{errors.email.message}</p>}
        </div>

        <div className="space-y-1">
          <Label htmlFor="displayName">Display name (optional)</Label>
          <Input id="displayName" {...register('displayName')} />
        </div>

        <div className="flex gap-3">
          <Button type="submit" disabled={isSubmitting || mutation.isPending}>
            Create user
          </Button>
          <Button type="button" variant="outline" onClick={() => { void navigate({ to: '/admin/users' }); }}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
