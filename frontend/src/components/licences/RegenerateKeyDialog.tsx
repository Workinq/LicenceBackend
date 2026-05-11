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

export function RegenerateKeyDialog({ licenceId }: { licenceId: string }) {
  const queryClient = useQueryClient();
  const [newKey, setNewKey] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => regenerateLicenceKey(licenceId, { reason: null }),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'detail', licenceId] });
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

  return (
    <>
      <ConfirmDestructive
        trigger={<Button variant="outline" disabled={mutation.isPending}>Regenerate key</Button>}
        title="Regenerate the key for this licence?"
        description="The current key stops working immediately and cannot be recovered. The client must be updated with the new key."
        confirmLabel="Regenerate key"
        onConfirm={() => { mutation.mutate(); }}
      />
      <Dialog open={newKey !== null} onOpenChange={(open) => { if (!open) setNewKey(null); }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New licence key</DialogTitle>
            <DialogDescription>The old key no longer works. Copy this now.</DialogDescription>
          </DialogHeader>
          {newKey !== null && <SecretRevealOnce label="New licence key" value={newKey} />}
          <DialogFooter>
            <Button onClick={() => { setNewKey(null); }}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
