import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { fetchLicenceSeats, forceRevokeSeat, updateLicenceMaxSeats } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceSeatsResponse } from '@/api/generated/api.schemas';

function errorDetail(error: unknown, fallback: string): string {
  return error instanceof ApiError &&
    error.body &&
    typeof error.body === 'object' &&
    'detail' in error.body
    ? String((error.body as Record<string, unknown>).detail)
    : fallback;
}

export function LicenceSeats({ licenceId }: { licenceId: string }) {
  const seatsQuery = useQuery({
    queryKey: ['licences', 'seats', licenceId],
    queryFn: () => fetchLicenceSeats(licenceId),
  });

  if (seatsQuery.isPending) return <Skeleton className="h-32 w-full" />;
  if (seatsQuery.isError || !seatsQuery.data) {
    return (
      <Alert variant="destructive">
        <AlertDescription>Failed to load seats.</AlertDescription>
      </Alert>
    );
  }

  return <LicenceSeatsView licenceId={licenceId} data={seatsQuery.data} />;
}

function LicenceSeatsView({
  licenceId,
  data,
}: {
  licenceId: string;
  data: LicenceSeatsResponse;
}) {
  const queryClient = useQueryClient();
  const [draftMaxSeats, setDraftMaxSeats] = useState<number>(data.maxSeats);

  const revokeMutation = useMutation({
    mutationFn: (seatId: string) => forceRevokeSeat(licenceId, seatId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'seats', licenceId] });
      toast.success('Seat revoked.');
    },
    onError: (error: unknown) => {
      toast.error(errorDetail(error, 'Could not revoke this seat.'));
    },
  });

  const maxSeatsMutation = useMutation({
    mutationFn: (next: number) =>
      updateLicenceMaxSeats(licenceId, { maxSeats: next, reason: null }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'seats', licenceId] });
      void queryClient.invalidateQueries({ queryKey: ['licences', 'detail', licenceId] });
      toast.success('Max seats updated.');
    },
    onError: (error: unknown) => {
      toast.error(errorDetail(error, 'Could not update max seats.'));
    },
  });

  const live = data.live;
  const history = data.history.items;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-4">
        <div>
          <Label className="text-xs text-ink-muted">Live seats</Label>
          <p className="flex h-8 items-center text-base font-medium text-ink">
            {live.length} of {data.maxSeats}
          </p>
        </div>
        <form
          className="flex items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            if (draftMaxSeats !== data.maxSeats) maxSeatsMutation.mutate(draftMaxSeats);
          }}
        >
          <div>
            <Label htmlFor="max-seats" className="text-xs text-ink-muted">
              Max seats
            </Label>
            <Input
              id="max-seats"
              type="number"
              min={1}
              max={1000}
              value={draftMaxSeats}
              onChange={(e) => { setDraftMaxSeats(Number(e.target.value)); }}
              disabled={maxSeatsMutation.isPending}
              className="h-8 w-24 text-sm"
            />
          </div>
          <Button
            type="submit"
            size="sm"
            disabled={
              maxSeatsMutation.isPending ||
              draftMaxSeats === data.maxSeats ||
              draftMaxSeats < 1 ||
              draftMaxSeats > 1000
            }
          >
            Save
          </Button>
        </form>
      </div>

      {live.length === 0 ? (
        <p className="text-sm text-ink-muted">No active seats.</p>
      ) : (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {live.map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-3 px-4 py-3">
              <div className="min-w-0 flex-1 space-y-0.5">
                <p className="truncate text-sm font-medium text-ink">
                  {s.instanceIdHashPrefix}{' '}
                  <span className="text-ink-muted">- {s.sourceIp}</span>
                </p>
                <p className="text-xs text-ink-muted">
                  Issued {new Date(s.issuedAt).toLocaleString()} - expires{' '}
                  {new Date(s.expiresAt).toLocaleString()}
                </p>
              </div>
              <ConfirmDestructive
                trigger={
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label="Revoke seat"
                    disabled={revokeMutation.isPending}
                  >
                    <Trash2 className="size-4" />
                  </Button>
                }
                title="Revoke this seat?"
                description={`Revoke seat ${s.instanceIdHashPrefix} from ${s.sourceIp}? The client will need to re-checkout.`}
                confirmLabel="Revoke seat"
                onConfirm={() => { revokeMutation.mutate(s.id); }}
              />
            </li>
          ))}
        </ul>
      )}

      {history.length > 0 && (
        <div className="space-y-2">
          <Label className="text-xs text-ink-muted">Recent history</Label>
          <ul className="divide-y divide-border rounded-lg border border-border">
            {history.slice(0, 10).map((h) => (
              <li key={h.id} className="px-4 py-2">
                <p className="truncate text-sm text-ink">
                  {h.instanceIdHashPrefix}{' '}
                  <span className="text-ink-muted">- {h.closeReason}</span>
                </p>
                <p className="text-xs text-ink-muted">
                  Closed {new Date(h.closedAt).toLocaleString()}
                </p>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
