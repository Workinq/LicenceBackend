import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { regenerateLicenceKey } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceKeyRegeneratedResponse } from '@/api/generated/api.schemas';

interface RegenerateKeyDialogProps {
  licenceId: string;
  regenerate?: (licenceId: string) => Promise<LicenceKeyRegeneratedResponse>;
  invalidateQueryKey?: readonly unknown[];
  hasKey?: boolean;
}

export function RegenerateKeyDialog({
  licenceId,
  regenerate,
  invalidateQueryKey,
  hasKey = true,
}: Readonly<RegenerateKeyDialogProps>) {
  const queryClient = useQueryClient();
  const [newKey, setNewKey] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      regenerate ? regenerate(licenceId) : regenerateLicenceKey(licenceId, { reason: null }),
    onSuccess: async (data) => {
      await queryClient.invalidateQueries({
        queryKey: invalidateQueryKey ?? ['licences', 'detail', licenceId],
      });
      setNewKey(data.licenceKey);
    },
    onError: (error) => {
      toast.error(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not regenerate the licence key.',
      );
    },
  });

  const actionLabel = hasKey ? 'Regenerate key' : 'Generate key';

  return (
    <>
      <ConfirmDestructive
        trigger={<Button variant="outline" disabled={mutation.isPending}>{actionLabel}</Button>}
        title={hasKey ? 'Regenerate the key for this licence?' : 'Generate a key for this licence?'}
        description={
          hasKey
            ? 'The current key stops working immediately and cannot be recovered. The client must be updated with the new key.'
            : 'The key is shown only once. Save it somewhere safe before closing the dialog.'
        }
        confirmLabel={actionLabel}
        onConfirm={() => { mutation.mutate(); }}
      />
      <Dialog open={newKey !== null} onOpenChange={(open) => { if (!open) setNewKey(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{hasKey ? 'New licence key' : 'Licence key'}</DialogTitle>
            <DialogDescription>
              {hasKey
                ? "The old key no longer works. You won't be able to see this new one again."
                : "You won't be able to see this key again."}
            </DialogDescription>
          </DialogHeader>
          {newKey !== null && <SecretRevealOnce label={hasKey ? 'New licence key' : 'Licence key'} value={newKey} />}
          <DialogFooter>
            <Button onClick={() => { setNewKey(null); }}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
