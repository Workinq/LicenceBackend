import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { StatusPill } from '@/components/StatusPill';
import { changePassword, fetchMe, updateProfile } from '@/api/me';
import { PASSWORD_MIN_LENGTH } from '@/api/policies.generated';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { ApiError } from '@/auth/api-client';

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '-';
}

export function ProfileEditor() {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const query = useQuery({ queryKey: ['me'], queryFn: fetchMe });

  const serverValue = query.data?.displayName ?? '';
  const [syncedFrom, setSyncedFrom] = useState(serverValue);
  const [displayName, setDisplayName] = useState(serverValue);
  if (query.data && serverValue !== syncedFrom) {
    setSyncedFrom(serverValue);
    setDisplayName(serverValue);
  }

  const mutation = useMutation({
    mutationFn: (next: string | null) => updateProfile({ displayName: next }),
    onSuccess: (user) => {
      setError(null);
      queryClient.setQueryData(['me'], user);
      const store = useAccessTokenStore.getState();
      if (store.user && store.user.id === user.id) {
        useAccessTokenStore.setState({ user: { ...store.user, displayName: user.displayName } });
      }
      toast.success('Profile updated.');
    },
    onError: (err: unknown) => {
      setError(
        err instanceof ApiError && err.body && typeof err.body === 'object' && 'detail' in err.body
          ? String((err.body as Record<string, unknown>).detail)
          : 'Could not update your profile.',
      );
    },
  });

  if (query.isPending) return <Skeleton className="h-64 w-full max-w-2xl" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load your profile.</p>;
  }

  const me = query.data;
  const original = me.displayName ?? '';
  const dirty = displayName !== original;

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">My profile</h1>

      <Card>
        <CardHeader>
          <CardTitle>Account</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[10rem_1fr] items-baseline gap-y-3 text-sm">
            <dt className="text-ink-muted">Email</dt>
            <dd className="text-ink">{me.email}</dd>
            <dt className="text-ink-muted">Role</dt>
            <dd><Badge variant="secondary" className="capitalize">{me.role}</Badge></dd>
            <dt className="text-ink-muted">Status</dt>
            <dd><StatusPill status={me.status} /></dd>
            <dt className="text-ink-muted">Created</dt>
            <dd className="text-ink">{formatDateTime(me.createdAt)}</dd>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Edit</CardTitle>
        </CardHeader>
        <CardContent>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              setError(null);
              const trimmed = displayName.trim();
              mutation.mutate(trimmed.length === 0 ? null : trimmed);
            }}
            className="space-y-4"
            noValidate
          >
            {error && (
              <Alert variant="destructive">
                <AlertDescription>{error}</AlertDescription>
              </Alert>
            )}
            <div className="space-y-1">
              <Label htmlFor="displayName">Display name</Label>
              <Input
                id="displayName"
                value={displayName}
                onChange={(e) => { setDisplayName(e.target.value); }}
                placeholder="Leave blank to clear"
                disabled={mutation.isPending}
                className="max-w-sm"
              />
              <p className="text-xs text-ink-muted">Shown to admins and on your account menu.</p>
            </div>
            <div className="flex gap-3">
              <Button type="submit" disabled={!dirty || mutation.isPending}>
                Save changes
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={!dirty || mutation.isPending}
                onClick={() => { setDisplayName(original); setError(null); }}
              >
                Reset
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <PasswordChangeCard />
    </div>
  );
}

function PasswordChangeCard() {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: (body: { currentPassword: string; newPassword: string }) => changePassword(body),
    onSuccess: () => {
      setError(null);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      toast.success('Password changed. Other sessions have been signed out.');
    },
    onError: (err: unknown) => {
      setError(
        err instanceof ApiError && err.body && typeof err.body === 'object' && 'detail' in err.body
          ? String((err.body as Record<string, unknown>).detail)
          : 'Could not change your password.',
      );
    },
  });

  const tooShort = newPassword.length > 0 && newPassword.length < PASSWORD_MIN_LENGTH;
  const mismatch = confirmPassword.length > 0 && newPassword !== confirmPassword;
  const canSubmit =
    currentPassword.length > 0 &&
    newPassword.length >= PASSWORD_MIN_LENGTH &&
    newPassword === confirmPassword &&
    !mutation.isPending;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setError(null);
            if (!canSubmit) return;
            mutation.mutate({ currentPassword, newPassword });
          }}
          className="space-y-4"
          noValidate
        >
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
          <div className="space-y-1">
            <Label htmlFor="currentPassword">Current password</Label>
            <Input
              id="currentPassword"
              type="password"
              autoComplete="current-password"
              value={currentPassword}
              onChange={(e) => { setCurrentPassword(e.target.value); }}
              disabled={mutation.isPending}
              className="max-w-sm"
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="newPassword">New password</Label>
            <Input
              id="newPassword"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => { setNewPassword(e.target.value); }}
              disabled={mutation.isPending}
              className="max-w-sm"
            />
            <p className="text-xs text-ink-muted">
              At least {PASSWORD_MIN_LENGTH} characters.
            </p>
            {tooShort && (
              <p className="text-xs text-status-revoked-fg">Password is too short.</p>
            )}
          </div>
          <div className="space-y-1">
            <Label htmlFor="confirmPassword">Confirm new password</Label>
            <Input
              id="confirmPassword"
              type="password"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => { setConfirmPassword(e.target.value); }}
              disabled={mutation.isPending}
              className="max-w-sm"
            />
            {mismatch && (
              <p className="text-xs text-status-revoked-fg">Passwords do not match.</p>
            )}
          </div>
          <div className="flex gap-3">
            <Button type="submit" disabled={!canSubmit}>
              Change password
            </Button>
          </div>
          <p className="text-xs text-ink-muted">
            Changing your password signs you out of every other device. This session stays signed in.
          </p>
        </form>
      </CardContent>
    </Card>
  );
}
