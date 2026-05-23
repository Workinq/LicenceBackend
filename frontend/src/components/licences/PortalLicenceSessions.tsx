import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { LogOut } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { fetchMyLicenceSeats } from '@/api/me-licences';
import { checkinSeat } from '@/api/checkouts';
import { ApiError } from '@/auth/api-client';

function errorDetail(error: unknown, fallback: string): string {
  return error instanceof ApiError &&
    error.body &&
    typeof error.body === 'object' &&
    'detail' in error.body
    ? String((error.body as Record<string, unknown>).detail)
    : fallback;
}

export function PortalLicenceSessions({ licenceId }: Readonly<{ licenceId: string }>) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['portal', 'licences', 'seats', licenceId],
    queryFn: () => fetchMyLicenceSeats(licenceId),
  });

  const signOutMutation = useMutation({
    mutationFn: (seatId: string) => checkinSeat(seatId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['portal', 'licences', 'seats', licenceId] });
      toast.success('Session signed out.');
    },
    onError: (error: unknown) => {
      toast.error(errorDetail(error, 'Could not sign out this session.'));
    },
  });

  if (query.isPending) return <Skeleton className="h-24 w-full" />;
  if (query.isError || !query.data) {
    return (
      <Alert variant="destructive">
        <AlertDescription>Failed to load active sessions.</AlertDescription>
      </Alert>
    );
  }

  const live = query.data.live;
  if (live.length === 0) {
    return <p className="text-sm text-ink-muted">No active sessions.</p>;
  }

  return (
    <ul className="divide-y divide-border rounded-lg border border-border">
      {live.map((s) => (
        <li key={s.id} className="flex items-center justify-between gap-3 px-4 py-3">
          <div className="min-w-0 flex-1 space-y-0.5">
            <p className="truncate text-sm font-medium text-ink">
              {s.instanceIdHashPrefix} <span className="text-ink-muted">- {s.sourceIp}</span>
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
                aria-label="Sign out"
                disabled={signOutMutation.isPending}
              >
                <LogOut className="size-4" />
              </Button>
            }
            title="Sign out this session?"
            description={`Sign out the session from ${s.sourceIp}? The client will lose its seat immediately and need to re-checkout.`}
            confirmLabel="Sign out"
            onConfirm={() => { signOutMutation.mutate(s.id); }}
          />
        </li>
      ))}
    </ul>
  );
}
