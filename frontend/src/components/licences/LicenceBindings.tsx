import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, X } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { updateLicenceHwid, updateLicenceIpAllowlist } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceResponse } from '@/api/generated/api.schemas';

function errorDetail(error: unknown, fallback: string): string {
  return error instanceof ApiError &&
    error.body &&
    typeof error.body === 'object' &&
    'detail' in error.body
    ? String((error.body as Record<string, unknown>).detail)
    : fallback;
}

interface Props {
  licence: LicenceResponse;
}

export function LicenceBindings({ licence }: Props) {
  const queryClient = useQueryClient();
  const [cidrs, setCidrs] = useState<string[]>(licence.ipAllowlist ?? []);

  const hwidMutation = useMutation({
    mutationFn: () => updateLicenceHwid(licence.id, { hwid: null, reason: null }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'detail', licence.id] });
      void queryClient.invalidateQueries({ queryKey: ['licences', 'list'] });
      toast.success('Hardware binding cleared.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not clear the hardware binding.'));
    },
  });

  const ipMutation = useMutation({
    mutationFn: () => {
      const cleaned = cidrs.map((c) => c.trim()).filter((c) => c.length > 0);
      return updateLicenceIpAllowlist(licence.id, {
        cidrs: cleaned.length > 0 ? cleaned : null,
        reason: null,
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'detail', licence.id] });
      void queryClient.invalidateQueries({ queryKey: ['licences', 'list'] });
      setCidrs((rows) => rows.map((c) => c.trim()).filter((c) => c.length > 0));
      toast.success('IP allowlist saved.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not save the IP allowlist.'));
    },
  });

  return (
    <div className="space-y-6">
      <div className="space-y-3">
        <h3 className="text-sm font-medium text-ink">Hardware (HWID)</h3>
        {licence.hwidBound ? (
          <>
            <p className="text-sm text-ink">Bound to a device.</p>
            <ConfirmDestructive
              trigger={<Button variant="outline">Clear HWID</Button>}
              title="Clear the hardware binding?"
              description="The licence will bind to whatever device it is next verified from."
              confirmLabel="Clear binding"
              onConfirm={() => { hwidMutation.mutate(); }}
            />
          </>
        ) : (
          <p className="text-sm text-ink-muted">Not bound to any device.</p>
        )}
      </div>

      <div className="space-y-3">
        <h3 className="text-sm font-medium text-ink">IP allowlist</h3>
        <p className="text-sm text-ink-muted">Leave empty to allow any IP.</p>
        <div className="space-y-2">
          {cidrs.map((c, i) => (
            <div key={i} className="flex items-center gap-2">
              <Input
                value={c}
                placeholder="CIDR e.g. 203.0.113.0/24"
                onChange={(e) => {
                  const v = e.target.value;
                  setCidrs((rows) => rows.map((r, j) => (j === i ? v : r)));
                }}
                className="flex-1"
              />
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Remove CIDR"
                onClick={() => { setCidrs((rows) => rows.filter((_, j) => j !== i)); }}
              >
                <X className="size-4" aria-hidden="true" />
              </Button>
            </div>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => { setCidrs((rows) => [...rows, '']); }}
          >
            <Plus className="size-4" aria-hidden="true" />
            <span className="ml-1.5">Add CIDR</span>
          </Button>
          <Button
            type="button"
            onClick={() => { ipMutation.mutate(); }}
            disabled={ipMutation.isPending}
          >
            Save allowlist
          </Button>
        </div>
      </div>
    </div>
  );
}
