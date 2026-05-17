import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { History, Shield, UserCog } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { StatusPill } from '@/components/StatusPill';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { AuditTimeline, type AuditEvent } from '@/components/AuditTimeline';
import { fetchUser, fetchUserLicences, updateUserStatus } from '@/api/users';
import { fetchAuditEvents } from '@/api/audit-events';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { ApiError } from '@/auth/api-client';
import type { AuditEventResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/admin/users_/$id')({
  component: UserDetailPage,
});

const LICENCE_PAGE = 10;
const AUDIT_PAGE = 20;

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : 'Never';
}

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '-';
}

function payloadString(payload: Record<string, unknown> | null, key: string): string {
  const v = payload?.[key];
  return typeof v === 'string' ? v : '?';
}

function describeAuditEvent(event: AuditEventResponse): { title: string; meta?: string } {
  const payload = event.payload as Record<string, unknown> | null;
  const meta = [event.actorUserEmail, event.reason].filter(Boolean).join(' - ') || undefined;
  switch (event.eventType) {
    case 'user.status_changed':
      return {
        title: `Status: ${payloadString(payload, 'previousStatus')} -> ${payloadString(payload, 'newStatus')}`,
        meta,
      };
    case 'user.role_changed':
      return {
        title: `Role: ${payloadString(payload, 'previousRole')} -> ${payloadString(payload, 'newRole')}`,
        meta,
      };
    default:
      return { title: event.eventType, meta };
  }
}

function iconFor(eventType: string) {
  if (eventType === 'user.role_changed') return UserCog;
  if (eventType === 'user.status_changed') return Shield;
  return History;
}

function UserDetailPage() {
  const { id } = Route.useParams();
  const queryClient = useQueryClient();
  const isSelf = useAccessTokenStore((state) => state.user?.id === id);
  const [actionError, setActionError] = useState<string | null>(null);
  const [suspendReason, setSuspendReason] = useState('');
  const [licenceOffset, setLicenceOffset] = useState(0);
  const [auditOffset, setAuditOffset] = useState(0);

  const userQuery = useQuery({ queryKey: ['users', 'detail', id], queryFn: () => fetchUser(id) });

  const licencesQuery = useQuery({
    queryKey: ['users', 'licences', id, licenceOffset],
    queryFn: () => fetchUserLicences(id, { limit: LICENCE_PAGE, offset: licenceOffset }),
    placeholderData: keepPreviousData,
  });

  const auditQuery = useQuery({
    queryKey: ['users', 'audit', id, auditOffset],
    queryFn: () =>
      fetchAuditEvents({ subject_type: 'user', subject_id: id, limit: AUDIT_PAGE, offset: auditOffset }),
    placeholderData: keepPreviousData,
  });

  const mutation = useMutation({
    mutationFn: ({ status, reason }: { status: string; reason: string | null }) =>
      updateUserStatus(id, { status, reason }),
    onSuccess: () => {
      setActionError(null);
      setSuspendReason('');
      void queryClient.invalidateQueries({ queryKey: ['users', 'detail', id] });
      void queryClient.invalidateQueries({ queryKey: ['users', 'audit', id] });
      void queryClient.invalidateQueries({ queryKey: ['users', 'list'] });
    },
    onError: (error) => {
      setActionError(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not update the user status.',
      );
    },
  });

  if (userQuery.isPending) return <Skeleton className="h-64 w-full max-w-3xl" />;
  if (userQuery.isError || !userQuery.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this user.</p>;
  }
  const user = userQuery.data;

  const auditEvents: AuditEvent[] = (auditQuery.data?.items ?? []).map((e) => {
    const described = describeAuditEvent(e);
    return {
      id: e.id,
      icon: iconFor(e.eventType),
      title: described.title,
      meta: described.meta,
      timestamp: e.occurredAt,
    };
  });

  const auditData = auditQuery.data;
  const licenceData = licencesQuery.data;

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex items-center gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">{user.email}</h1>
        <StatusPill status={user.status} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Profile</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[10rem_1fr] items-baseline gap-y-3 text-sm">
            <dt className="text-ink-muted">ID</dt>
            <dd className="font-mono text-xs text-ink">{user.id}</dd>
            <dt className="text-ink-muted">Display name</dt>
            <dd className="text-ink">{user.displayName ?? '-'}</dd>
            <dt className="text-ink-muted">Role</dt>
            <dd className="capitalize text-ink">{user.role}</dd>
            <dt className="text-ink-muted">Status</dt>
            <dd><StatusPill status={user.status} /></dd>
            <dt className="text-ink-muted">Created</dt>
            <dd className="text-ink">{formatDateTime(user.createdAt)}</dd>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Actions</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {actionError && (
            <Alert variant="destructive">
              <AlertDescription>{actionError}</AlertDescription>
            </Alert>
          )}
          <div className="flex flex-wrap gap-3">
            {user.status === 'active' ? (
              isSelf ? (
                <span title="You cannot suspend your own account." className="inline-block">
                  <Button variant="destructive" disabled className="pointer-events-none">
                    Suspend
                  </Button>
                </span>
              ) : (
                <ConfirmDestructive
                  trigger={
                    <Button variant="destructive" disabled={mutation.isPending}>
                      Suspend
                    </Button>
                  }
                  title="Suspend user"
                  description={`This will block ${user.email} from logging in and revoke all of their refresh tokens.`}
                  confirmLabel="Suspend user"
                  onConfirm={() => { mutation.mutate({ status: 'suspended', reason: suspendReason.trim() || null }); }}
                >
                  <div className="space-y-1">
                    <Label htmlFor="suspend-reason">Reason (optional)</Label>
                    <Textarea
                      id="suspend-reason"
                      rows={2}
                      value={suspendReason}
                      onChange={(e) => { setSuspendReason(e.target.value); }}
                    />
                  </div>
                </ConfirmDestructive>
              )
            ) : (
              <Button
                disabled={mutation.isPending}
                onClick={() => { mutation.mutate({ status: 'active', reason: null }); }}
              >
                Reactivate
              </Button>
            )}
            <span title="Coming in Chunk J - password reset infrastructure" className="inline-block">
              <Button variant="outline" disabled className="pointer-events-none">
                Reset password
              </Button>
            </span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Licences</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {licencesQuery.isPending && <Skeleton className="h-12 w-full" />}
          {licencesQuery.isError && (
            <p className="text-sm text-status-revoked-fg">Failed to load licences.</p>
          )}
          {licenceData && licenceData.items.length === 0 && (
            <p className="text-sm text-ink-muted">This user has no licences.</p>
          )}
          {licenceData && licenceData.items.length > 0 && (
            <>
              <div className="overflow-hidden rounded-lg border border-border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Product</TableHead>
                      <TableHead>Relationship</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Expires</TableHead>
                      <TableHead>Created</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {licenceData.items.map((lic) => (
                      <TableRow key={lic.id}>
                        <TableCell>
                          <Link
                            to="/admin/licences/$id"
                            params={{ id: lic.id }}
                            className="font-medium text-ink underline-offset-2 hover:underline"
                          >
                            {lic.productSlug}
                          </Link>
                        </TableCell>
                        <TableCell>
                          <Badge variant={lic.relationship === 'owner' ? 'default' : 'secondary'} className="capitalize">
                            {lic.relationship ?? 'owner'}
                          </Badge>
                        </TableCell>
                        <TableCell><StatusPill status={lic.status} /></TableCell>
                        <TableCell className="text-ink-muted">{formatDate(lic.expiresAt)}</TableCell>
                        <TableCell className="text-ink-muted">{formatDate(lic.createdAt)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              {licenceData.total > LICENCE_PAGE && (
                <div className="flex items-center justify-between text-sm text-ink-muted">
                  <span>
                    {licenceData.offset + 1}-{Math.min(licenceData.offset + licenceData.limit, licenceData.total)} of {licenceData.total}
                  </span>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={licenceOffset === 0}
                      onClick={() => { setLicenceOffset(Math.max(0, licenceOffset - LICENCE_PAGE)); }}
                    >
                      Previous
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={licenceOffset + LICENCE_PAGE >= licenceData.total}
                      onClick={() => { setLicenceOffset(licenceOffset + LICENCE_PAGE); }}
                    >
                      Next
                    </Button>
                  </div>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>History</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <AuditTimeline
            events={auditEvents}
            isLoading={auditQuery.isPending}
            isError={auditQuery.isError}
            emptyText="No activity yet."
          />
          {auditData && auditData.total > AUDIT_PAGE && (
            <div className="flex items-center justify-between text-sm text-ink-muted">
              <span>
                {auditData.offset + 1}-{Math.min(auditData.offset + auditData.limit, auditData.total)} of {auditData.total}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={auditOffset === 0}
                  onClick={() => { setAuditOffset(Math.max(0, auditOffset - AUDIT_PAGE)); }}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={auditOffset + AUDIT_PAGE >= auditData.total}
                  onClick={() => { setAuditOffset(auditOffset + AUDIT_PAGE); }}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
