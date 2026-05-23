import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { addLicenceMember, fetchLicenceMembers, removeLicenceMember } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceMemberResponse } from '@/api/generated/api.schemas';

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function errorDetail(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body) {
    return String((error.body as Record<string, unknown>).detail);
  }
  return fallback;
}

export function LicenceMembers({ licenceId }: Readonly<{ licenceId: string }>) {
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [addError, setAddError] = useState<string | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['licences', 'members', licenceId],
    queryFn: () => fetchLicenceMembers(licenceId),
  });

  const addMutation = useMutation({
    mutationFn: (memberEmail: string) => addLicenceMember(licenceId, { email: memberEmail }),
    onSuccess: async () => {
      setEmail('');
      setAddError(null);
      await queryClient.invalidateQueries({ queryKey: ['licences', 'members', licenceId] });
    },
    onError: (error: unknown) => {
      setAddError(errorDetail(error, 'Could not add the member.'));
    },
  });

  const removeMutation = useMutation({
    mutationFn: (memberId: string) => removeLicenceMember(licenceId, memberId),
    onSuccess: async () => {
      setRemoveError(null);
      await queryClient.invalidateQueries({ queryKey: ['licences', 'members', licenceId] });
    },
    onError: (error: unknown) => {
      setRemoveError(errorDetail(error, 'Could not remove the member.'));
    },
  });

  const onSubmit = (e: React.SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    setAddError(null);
    const trimmed = email.trim();
    if (!trimmed) return;
    addMutation.mutate(trimmed);
  };

  return (
    <div className="space-y-4">
      <form onSubmit={onSubmit} className="space-y-2" noValidate>
        <Label htmlFor="member-email">Add member by email</Label>
        <div className="flex gap-2">
          <Input
            id="member-email"
            type="email"
            placeholder="user@example.com"
            value={email}
            onChange={(e) => { setEmail(e.target.value); }}
            disabled={addMutation.isPending}
            className="max-w-sm"
          />
          <Button type="submit" disabled={addMutation.isPending || email.trim().length === 0}>
            Add
          </Button>
        </div>
        {addError && (
          <Alert variant="destructive">
            <AlertDescription>{addError}</AlertDescription>
          </Alert>
        )}
      </form>

      {removeError && (
        <Alert variant="destructive">
          <AlertDescription>{removeError}</AlertDescription>
        </Alert>
      )}

      {query.isPending && <Skeleton className="h-10 w-full" />}
      {query.isError && (
        <p className="text-sm text-status-revoked-fg">Failed to load members.</p>
      )}
      {query.data?.length === 0 && (
        <p className="text-sm text-ink-muted">No members yet. Add one by email above.</p>
      )}
      {query.data && query.data.length > 0 && (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {query.data.map((member: LicenceMemberResponse) => (
            <li key={member.userId} className="flex items-center justify-between gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-ink">{member.email}</p>
                <p className="text-xs text-ink-muted">
                  Added by {member.addedByEmail ?? 'unknown'} on {formatDate(member.addedAt)}
                </p>
              </div>
              <ConfirmDestructive
                trigger={
                  <Button variant="ghost" size="icon" disabled={removeMutation.isPending} aria-label={`Remove ${member.email}`}>
                    <Trash2 className="size-4" />
                  </Button>
                }
                title="Remove member"
                description={`Remove ${member.email} from this licence?`}
                confirmLabel="Remove"
                onConfirm={() => { removeMutation.mutate(member.userId); }}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
