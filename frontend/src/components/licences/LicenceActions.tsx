import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { RegenerateKeyDialog } from './RegenerateKeyDialog';
import { updateLicenceStatus } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceResponse } from '@/api/generated/api.schemas';

export function LicenceActions({ licence }: Readonly<{ licence: LicenceResponse }>) {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: (status: string) => updateLicenceStatus(licence.id, { status, reason: null }),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['licences', 'detail', licence.id] }),
        queryClient.invalidateQueries({ queryKey: ['licences', 'list'] }),
      ]);
      toast.success('Licence updated.');
    },
    onError: (error) => {
      toast.error(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not update the licence.',
      );
    },
  });

  const revokeAction = (
    <ConfirmDestructive
      trigger={<Button variant="destructive" disabled={mutation.isPending}>Revoke</Button>}
      title="Revoke this licence?"
      description="Revoking is permanent. The client will stop validating immediately."
      confirmLabel="Revoke licence"
      onConfirm={() => { mutation.mutate('revoked'); }}
    />
  );

  if (licence.status === 'active') {
    return (
      <div className="flex gap-3">
        <ConfirmDestructive
          trigger={<Button variant="outline" disabled={mutation.isPending}>Suspend</Button>}
          title="Suspend this licence?"
          description="The client will fail validation until the licence is reinstated."
          confirmLabel="Suspend licence"
          onConfirm={() => { mutation.mutate('suspended'); }}
        />
        {revokeAction}
        <RegenerateKeyDialog licenceId={licence.id} />
      </div>
    );
  }

  if (licence.status === 'suspended') {
    return (
      <div className="flex gap-3">
        <Button onClick={() => { mutation.mutate('active'); }} disabled={mutation.isPending}>
          Reinstate
        </Button>
        {revokeAction}
      </div>
    );
  }

  if (licence.status === 'revoked') {
    return <p className="text-sm text-ink-muted">This licence has been revoked and cannot be changed.</p>;
  }

  return <p className="text-sm text-ink-muted">No actions available for status {licence.status}.</p>;
}
