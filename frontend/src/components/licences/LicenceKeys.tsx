import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Check, Pencil, Trash2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import {
  fetchLicenceKeys,
  mintLicenceKey,
  revokeLicenceKey,
  updateLicenceKeyLabel,
} from '@/api/licence-keys';
import { ApiError } from '@/auth/api-client';
import type { LicenceKeyResponse } from '@/api/generated/api.schemas';

interface LicenceKeysProps {
  licenceId: string;
  canMutate: boolean;
}

function problemTitle(error: unknown): string | null {
  if (
    error instanceof ApiError &&
    error.body &&
    typeof error.body === 'object' &&
    'title' in error.body
  ) {
    return String((error.body as Record<string, unknown>).title);
  }
  return null;
}

function formatDateTime(value: string | null): string {
  return value === null ? 'never' : new Date(value).toLocaleString();
}

export function LicenceKeys({ licenceId, canMutate }: Readonly<LicenceKeysProps>) {
  const queryClient = useQueryClient();
  const [revealedKey, setRevealedKey] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ['licences', 'keys', licenceId],
    queryFn: () => fetchLicenceKeys(licenceId, {}),
  });

  const mintMutation = useMutation({
    mutationFn: () => mintLicenceKey(licenceId, { label: null, reason: null }),
    onSuccess: async (data) => {
      await queryClient.invalidateQueries({ queryKey: ['licences', 'keys', licenceId] });
      setRevealedKey(data.licenceKey);
    },
    onError: (error: unknown) => {
      if (problemTitle(error) === 'licence_key_cap_exceeded') {
        toast.error('Maximum active keys reached. Revoke one first.');
        return;
      }
      toast.error('Could not generate the key.');
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (keyId: string) => revokeLicenceKey(licenceId, keyId, null),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['licences', 'keys', licenceId] }),
        queryClient.invalidateQueries({ queryKey: ['licences', 'seats', licenceId] }),
      ]);
      toast.success('Key revoked.');
    },
    onError: () => {
      toast.error('Could not revoke the key.');
    },
  });

  const labelMutation = useMutation({
    mutationFn: ({ keyId, label }: { keyId: string; label: string | null }) =>
      updateLicenceKeyLabel(licenceId, keyId, { label, reason: null }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['licences', 'keys', licenceId] });
      toast.success('Label updated.');
    },
    onError: () => {
      toast.error('Could not update the label.');
    },
  });

  if (query.isPending) return <Skeleton className="h-32 w-full" />;
  if (query.isError || !query.data) {
    return (
      <Alert variant="destructive">
        <AlertDescription>Failed to load keys.</AlertDescription>
      </Alert>
    );
  }

  const { activeCount, activeCap, keys } = query.data;
  const atCap = activeCount >= activeCap;
  const generateDisabled = atCap || mintMutation.isPending;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-ink-muted">
          {activeCount}/{activeCap} active key{activeCap === 1 ? '' : 's'}
        </p>
        {canMutate && (
          <Button
            onClick={() => { mintMutation.mutate(); }}
            disabled={generateDisabled}
          >
            Generate new key
          </Button>
        )}
      </div>

      {keys.length === 0 ? (
        <p className="text-sm text-ink-muted">No active keys yet.</p>
      ) : (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {keys.map((key) => (
            <LicenceKeyRow
              key={key.id}
              entry={key}
              canMutate={canMutate}
              onRevoke={() => { revokeMutation.mutate(key.id); }}
              onLabelSave={(label) => { labelMutation.mutate({ keyId: key.id, label }); }}
              revoking={revokeMutation.isPending}
              labelPending={labelMutation.isPending}
            />
          ))}
        </ul>
      )}

      <Dialog
        open={revealedKey !== null}
        onOpenChange={(open) => { if (!open) setRevealedKey(null); }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>New licence key</DialogTitle>
            <DialogDescription>
              You won't be able to see this key again. Copy it somewhere safe before closing.
            </DialogDescription>
          </DialogHeader>
          {revealedKey !== null && (
            <SecretRevealOnce label="New licence key" value={revealedKey} />
          )}
          <DialogFooter>
            <Button onClick={() => { setRevealedKey(null); }}>Done</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

interface LicenceKeyRowProps {
  entry: LicenceKeyResponse;
  canMutate: boolean;
  onRevoke: () => void;
  onLabelSave: (label: string | null) => void;
  revoking: boolean;
  labelPending: boolean;
}

function LicenceKeyRow({
  entry,
  canMutate,
  onRevoke,
  onLabelSave,
  revoking,
  labelPending,
}: Readonly<LicenceKeyRowProps>) {
  const revoked = entry.revokedAt !== null;

  let trailing: React.ReactNode = null;
  if (revoked) {
    trailing = <span className="text-xs text-ink-muted">revoked</span>;
  } else if (canMutate) {
    trailing = (
      <ConfirmDestructive
        trigger={
          <Button
            variant="ghost"
            size="icon"
            aria-label="Revoke key"
            disabled={revoking}
          >
            <Trash2 className="size-4" />
          </Button>
        }
        title="Revoke this key?"
        description={`Revoke key ${entry.keyPrefix}? The key stops working immediately and cannot be recovered.`}
        confirmLabel="Revoke key"
        onConfirm={onRevoke}
      />
    );
  }

  return (
    <li className="flex items-center justify-between gap-3 px-4 py-3">
      <div className="min-w-0 flex-1 space-y-1">
        <p className="font-mono text-[12px] text-ink">{entry.keyPrefix}</p>
        <KeyLabelEditor
          label={entry.label}
          editable={canMutate && !revoked}
          pending={labelPending}
          onSave={onLabelSave}
        />
        <p className="text-xs text-ink-muted">
          Created {formatDateTime(entry.createdAt)} - Last used {formatDateTime(entry.lastSeenAt)}
          {revoked && <> - Revoked {formatDateTime(entry.revokedAt)}</>}
        </p>
      </div>
      {trailing}
    </li>
  );
}

interface KeyLabelEditorProps {
  label: string | null;
  editable: boolean;
  pending: boolean;
  onSave: (label: string | null) => void;
}

function KeyLabelEditor({ label, editable, pending, onSave }: Readonly<KeyLabelEditorProps>) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(label ?? '');

  if (!editable) {
    return (
      <span className="text-sm text-ink">
        {label ?? <span className="text-ink-subtle">-</span>}
      </span>
    );
  }

  if (!editing) {
    return (
      <div className="flex items-center gap-2">
        <span className="text-sm text-ink">
          {label ?? <span className="text-ink-subtle">-</span>}
        </span>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Edit label"
          className="size-7"
          onClick={() => {
            setDraft(label ?? '');
            setEditing(true);
          }}
        >
          <Pencil className="size-3.5" aria-hidden="true" />
        </Button>
      </div>
    );
  }

  const save = () => {
    const trimmed = draft.trim();
    onSave(trimmed === '' ? null : trimmed);
    setEditing(false);
  };

  return (
    <div className="flex items-center gap-2">
      <Input
        value={draft}
        maxLength={32}
        onChange={(e) => { setDraft(e.target.value); }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.preventDefault();
            save();
          }
          if (e.key === 'Escape') {
            setEditing(false);
          }
        }}
        placeholder="No label"
        autoFocus
        className="h-8 text-sm"
        disabled={pending}
      />
      <Button
        variant="outline"
        size="icon"
        aria-label="Save label"
        className="size-8"
        disabled={pending}
        onClick={save}
      >
        <Check className="size-3.5" aria-hidden="true" />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        aria-label="Cancel label edit"
        className="size-8"
        disabled={pending}
        onClick={() => { setEditing(false); }}
      >
        <X className="size-3.5" aria-hidden="true" />
      </Button>
    </div>
  );
}
