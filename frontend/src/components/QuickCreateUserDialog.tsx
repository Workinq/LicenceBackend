import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import { createUser } from '@/api/users';
import { generatePassword } from '@/lib/generate-password';
import { ApiError } from '@/auth/api-client';
import type { UserResponse } from '@/api/generated/api.schemas';

const schema = z.object({
  email: z
    .string()
    .min(1, 'Required')
    .pipe(z.email('Enter a valid email')),
  displayName: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

interface QuickCreateUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (user: UserResponse) => void;
}

interface Created {
  user: UserResponse;
  password: string;
}

export function QuickCreateUserDialog({ open, onOpenChange, onCreated }: Readonly<QuickCreateUserDialogProps>) {
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [created, setCreated] = useState<Created | null>(null);

  const {
    register,
    handleSubmit,
    reset,
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
    try {
      await mutation.mutateAsync(values);
    } catch {
      // onError handler on the mutation already surfaces the message via setSubmitError; swallow here.
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      reset();
      setSubmitError(null);
      setCreated(null);
    }
    onOpenChange(next);
  };

  const onDone = () => {
    if (created) {
      onCreated(created.user);
    }
    handleOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        {created ? (
          <>
            <DialogHeader>
              <DialogTitle>User created</DialogTitle>
              <DialogDescription>
                Share this password with {created.user.email}. It will not be shown again.
              </DialogDescription>
            </DialogHeader>
            <SecretRevealOnce label="Initial password" value={created.password} />
            <DialogFooter>
              <Button type="button" onClick={onDone}>
                Done
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>New user</DialogTitle>
              <DialogDescription>
                A random password is generated and shown once after creation.
              </DialogDescription>
            </DialogHeader>

            <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
              {submitError && (
                <Alert variant="destructive">
                  <AlertDescription>{submitError}</AlertDescription>
                </Alert>
              )}

              <div className="space-y-1">
                <Label htmlFor="qc-user-email">Email</Label>
                <Input
                  id="qc-user-email"
                  type="email"
                  autoComplete="off"
                  placeholder="user@example.com"
                  {...register('email')}
                />
                {errors.email && <p className="text-xs text-status-revoked-fg">{errors.email.message}</p>}
              </div>

              <div className="space-y-1">
                <Label htmlFor="qc-user-display">Display name (optional)</Label>
                <Input id="qc-user-display" {...register('displayName')} />
              </div>

              <DialogFooter>
                <Button type="button" variant="outline" onClick={() => { handleOpenChange(false); }}>
                  Cancel
                </Button>
                <Button type="submit" disabled={isSubmitting || mutation.isPending}>
                  Create user
                </Button>
              </DialogFooter>
            </form>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
