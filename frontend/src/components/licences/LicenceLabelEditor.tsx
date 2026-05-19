import { useState } from 'react';
import { Check, Pencil, X } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { updateMyLicenceLabel } from '@/api/me-licences';

interface LicenceLabelEditorProps {
  licenceId: string;
  label: string | null;
  editable: boolean;
}

export function LicenceLabelEditor({ licenceId, label, editable }: LicenceLabelEditorProps) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(label ?? '');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async (next: string | null) =>
      updateMyLicenceLabel(licenceId, { label: next }),
    onSuccess: () => {
      setEditing(false);
      setError(null);
      void queryClient.invalidateQueries({ queryKey: ['portal', 'licences'] });
      void queryClient.invalidateQueries({ queryKey: ['portal', 'licence', licenceId] });
      void queryClient.invalidateQueries({ queryKey: ['portal', 'orders'] });
    },
    onError: (err: unknown) => {
      setError(err instanceof Error ? err.message : 'Failed to update label.');
    },
  });

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
            setError(null);
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
    mutation.mutate(trimmed === '' ? null : trimmed);
  };

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-2">
        <Input
          value={draft}
          maxLength={10}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              save();
            }
            if (e.key === 'Escape') {
              setEditing(false);
              setError(null);
            }
          }}
          placeholder="No label"
          autoFocus
          className="h-8 text-sm"
          disabled={mutation.isPending}
        />
        <Button variant="outline" size="icon" aria-label="Save" className="size-8" disabled={mutation.isPending} onClick={save}>
          <Check className="size-3.5" aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Cancel"
          className="size-8"
          disabled={mutation.isPending}
          onClick={() => {
            setEditing(false);
            setError(null);
          }}
        >
          <X className="size-3.5" aria-hidden="true" />
        </Button>
      </div>
      {error && <p className="text-xs text-status-revoked-fg">{error}</p>}
    </div>
  );
}
