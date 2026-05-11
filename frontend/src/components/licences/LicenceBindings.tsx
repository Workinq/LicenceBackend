import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { CidrListEditor } from '@/components/licences/CidrListEditor';
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
  const [restricted, setRestricted] = useState(licence.ipAllowlist != null);
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
      if (!restricted) {
        return updateLicenceIpAllowlist(licence.id, { cidrs: null, reason: null });
      }
      return updateLicenceIpAllowlist(licence.id, {
        cidrs: cidrs.map((c) => c.trim()).filter((c) => c.length > 0),
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
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-ink">IP allowlist</h3>
          <div className="flex items-center gap-2">
            <Switch
              id="ip-restrict"
              checked={restricted}
              onCheckedChange={setRestricted}
              aria-label="Restrict by IP address"
            />
            <Label htmlFor="ip-restrict">Restrict by IP address</Label>
          </div>
        </div>
        {restricted ? (
          <>
            <p className="text-sm text-ink-muted">
              Leave empty and the first IP that verifies this licence will be locked in automatically.
            </p>
            <CidrListEditor cidrs={cidrs} onChange={setCidrs} />
          </>
        ) : (
          <p className="text-sm text-ink-muted">IP restriction is off. Any IP can verify this licence.</p>
        )}
        <Button
          type="button"
          onClick={() => { ipMutation.mutate(); }}
          disabled={ipMutation.isPending}
        >
          Save allowlist
        </Button>
      </div>
    </div>
  );
}
