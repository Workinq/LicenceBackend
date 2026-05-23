import { useMemo, useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { ChevronLeft, ChevronRight, History, Shield, UserCog } from 'lucide-react';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { StatusPill } from '@/components/StatusPill';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import { AuditTimeline, type AuditEvent } from '@/components/AuditTimeline';
import { KeyChip } from '@/components/dashboard/KeyChip';
import { fetchUser, fetchUserLicences, updateUserStatus } from '@/api/users';
import { fetchAuditEvents } from '@/api/audit-events';
import { useAccessTokenStore } from '@/auth/access-token-store';
import { ApiError } from '@/auth/api-client';
import { cn } from '@/lib/utils';
import { formatDate, formatRelative } from '@/lib/format';
import type { AuditEventResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/admin/users_/$id')({
  component: UserDetailPage,
});

const LICENCE_PAGE = 10;
const AUDIT_PAGE = 20;

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
  const navigate = useNavigate();
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

  const allLicencesQuery = useQuery({
    queryKey: ['users', 'licences-all', id],
    queryFn: () => fetchUserLicences(id, { limit: 1, offset: 0 }),
    staleTime: 30_000,
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

  const auditEvents = useMemo<AuditEvent[]>(
    () =>
      (auditQuery.data?.items ?? []).map((e) => {
        const described = describeAuditEvent(e);
        return {
          id: e.id,
          icon: iconFor(e.eventType),
          title: described.title,
          meta: described.meta,
          timestamp: e.occurredAt,
        };
      }),
    [auditQuery.data],
  );

  if (userQuery.isPending) return <Skeleton className="h-64 w-full" />;
  if (userQuery.isError || !userQuery.data) {
    return <p className="text-[12.5px] text-status-revoked-fg">Failed to load this user.</p>;
  }
  const user = userQuery.data;

  const auditData = auditQuery.data;
  const licenceData = licencesQuery.data;
  const totalLicences = allLicencesQuery.data?.total ?? licenceData?.total ?? 0;
  const lastActivity = auditData?.items[0]?.occurredAt;

  const initial = (user.displayName ?? user.email).charAt(0).toUpperCase();
  const suspendButton =
    user.status === 'active' ? (
      isSelf ? (
        <span title="You cannot suspend your own account." className="inline-block">
          <Button variant="destructive" size="sm" disabled className="pointer-events-none">
            Suspend
          </Button>
        </span>
      ) : (
        <ConfirmDestructive
          trigger={
            <Button variant="destructive" size="sm" disabled={mutation.isPending}>
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
        size="sm"
        disabled={mutation.isPending}
        onClick={() => { mutation.mutate({ status: 'active', reason: null }); }}
      >
        Reactivate
      </Button>
    );

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-md border border-border bg-surface-sunken font-mono text-[14px] font-semibold text-foreground">
            {initial}
          </span>
          <h1 className="text-[20px] font-semibold tracking-tight text-foreground">{user.email}</h1>
          <StatusPill status={user.status} />
          <RoleBadge role={user.role} />
          <div className="ml-auto flex items-center gap-2">
            {suspendButton}
            <span title="Coming in Chunk J - password reset infrastructure" className="inline-block">
              <Button variant="outline" size="sm" disabled className="pointer-events-none">
                Reset password
              </Button>
            </span>
          </div>
        </div>
        <p className="text-[12px] text-ink-muted">
          User ID <KeyChip value={user.id} display={user.id.slice(0, 20)} className="ml-1" />
          {user.displayName && (
            <>
              <span aria-hidden className="mx-1.5 text-ink-subtle">·</span>
              <span className="text-foreground">{user.displayName}</span>
            </>
          )}
        </p>
      </header>

      <div className="grid grid-cols-2 gap-px overflow-hidden rounded-md border border-border bg-border text-[12.5px] sm:grid-cols-3 lg:grid-cols-5">
        <StatCell label="Licences" value={totalLicences.toLocaleString()} />
        <StatCell label="Role" value={user.role} />
        <StatCell label="Status" value={user.status} />
        <StatCell label="Last activity" value={lastActivity ? formatRelative(lastActivity) : 'None'} />
        <StatCell label="Created" value={formatDate(user.createdAt)} />
      </div>

      {actionError && (
        <Alert variant="destructive">
          <AlertDescription>{actionError}</AlertDescription>
        </Alert>
      )}

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-[360px_1fr]">
        <DetailCard title="Profile">
          <dl className="grid grid-cols-[80px_1fr] gap-y-2.5 text-[12.5px]">
            <dt className="text-ink-muted">ID</dt>
            <dd>
              <KeyChip value={user.id} display={user.id.slice(0, 14)} />
            </dd>
            <dt className="text-ink-muted">Email</dt>
            <dd className="truncate">{user.email}</dd>
            <dt className="text-ink-muted">Name</dt>
            <dd>{user.displayName ?? <span className="text-ink-subtle">-</span>}</dd>
            <dt className="text-ink-muted">Role</dt>
            <dd>
              <RoleBadge role={user.role} />
            </dd>
            <dt className="text-ink-muted">Status</dt>
            <dd>
              <StatusPill status={user.status} />
            </dd>
            <dt className="text-ink-muted">Created</dt>
            <dd className="font-mono text-[11.5px] text-ink-muted">{formatDate(user.createdAt)}</dd>
          </dl>
        </DetailCard>

        <DetailCard title="Licences">
          <div className="-mx-4 -mt-4 overflow-hidden">
            <Table className="text-[12.5px]">
              <TableHeader>
                <TableRow className="border-border">
                  <Th>Product</Th>
                  <Th className="w-[110px]">Relationship</Th>
                  <Th className="w-[100px]">Status</Th>
                  <Th className="w-[110px]">Expires</Th>
                  <Th className="w-[110px]">Created</Th>
                  <Th className="w-[36px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {licencesQuery.isPending && (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <Skeleton className="h-6 w-full" />
                    </TableCell>
                  </TableRow>
                )}
                {licencesQuery.isError && (
                  <TableRow>
                    <TableCell colSpan={6} className="text-status-revoked-fg">
                      Failed to load licences.
                    </TableCell>
                  </TableRow>
                )}
                {licenceData?.items.map((lic) => {
                  const go = () => { void navigate({ to: '/admin/licences/$id', params: { id: lic.id } }); };
                  return (
                    <TableRow
                      key={lic.id}
                      role="link"
                      tabIndex={0}
                      onClick={go}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          go();
                        }
                      }}
                      aria-label={`View licence ${lic.productSlug}`}
                      className="group cursor-pointer border-border transition-colors hover:bg-accent-soft focus:bg-accent-soft focus:outline-none"
                    >
                      <Td>
                        <span className="font-mono text-[11.5px] font-medium text-accent group-hover:underline">
                          {lic.productSlug}
                        </span>
                      </Td>
                      <Td>
                        <span className="text-ink-muted">{lic.relationship ?? 'owner'}</span>
                      </Td>
                      <Td>
                        <StatusPill status={lic.status} />
                      </Td>
                      <Td className="font-mono text-[11.5px] text-ink-muted">
                        {lic.expiresAt ? formatDate(lic.expiresAt) : '-'}
                      </Td>
                      <Td className="font-mono text-[11.5px] text-ink-muted">{formatDate(lic.createdAt)}</Td>
                      <Td className="text-right">
                        <ChevronRight
                          className="size-3.5 text-ink-subtle transition-transform group-hover:translate-x-0.5 group-hover:text-accent"
                          aria-hidden
                        />
                      </Td>
                    </TableRow>
                  );
                })}
                {licenceData && licenceData.items.length === 0 && !licencesQuery.isError && (
                  <TableRow>
                    <TableCell colSpan={6} className="text-ink-muted">
                      This user has no licences.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          {licenceData && licenceData.total > LICENCE_PAGE && (
            <Pager
              offset={licenceOffset}
              limit={licenceData.limit}
              total={licenceData.total}
              pageSize={LICENCE_PAGE}
              onChange={setLicenceOffset}
            />
          )}
        </DetailCard>
      </div>

      <DetailCard title="History">
        <AuditTimeline
          events={auditEvents}
          isLoading={auditQuery.isPending}
          isError={auditQuery.isError}
          emptyText="No activity yet."
        />
        {auditData && auditData.total > AUDIT_PAGE && (
          <Pager
            offset={auditOffset}
            limit={auditData.limit}
            total={auditData.total}
            pageSize={AUDIT_PAGE}
            onChange={setAuditOffset}
          />
        )}
      </DetailCard>
    </div>
  );
}

function StatCell({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-card px-3 py-3">
      <div className="text-[10.5px] font-medium uppercase tracking-wide text-ink-muted">{label}</div>
      <div className="mt-1 text-[16px] font-semibold capitalize tabular-nums text-foreground">{value}</div>
    </div>
  );
}

function DetailCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="border-b border-border px-4 py-2.5">
        <h2 className="text-[13px] font-semibold text-foreground">{title}</h2>
      </div>
      <div className="p-4">{children}</div>
    </div>
  );
}

function RoleBadge({ role }: { role: string }) {
  const isAdmin = role === 'admin';
  return (
    <span
      className={cn(
        'inline-flex h-5 items-center rounded-[3px] border px-1.5 font-mono text-[10.5px] leading-none',
        isAdmin
          ? 'border-accent/30 bg-accent-soft text-accent'
          : 'border-border bg-surface-sunken text-ink-muted',
      )}
    >
      {role}
    </span>
  );
}

interface PagerProps {
  offset: number;
  limit: number;
  total: number;
  pageSize: number;
  onChange: (next: number) => void;
}

function Pager({ offset, limit, total, pageSize, onChange }: PagerProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const currentPage = Math.floor(offset / pageSize) + 1;
  const rangeStart = total > 0 ? offset + 1 : 0;
  const rangeEnd = Math.min(offset + limit, total);

  return (
    <div className="flex items-center justify-between border-t border-border pt-3 text-[12px] text-ink-muted">
      <span className="font-mono tabular-nums">
        {rangeStart}-{rangeEnd} of {total}
      </span>
      <div className="flex items-center gap-1.5">
        <Button
          variant="outline"
          size="icon"
          className="size-7"
          disabled={offset === 0}
          onClick={() => { onChange(Math.max(0, offset - pageSize)); }}
          aria-label="Previous page"
        >
          <ChevronLeft className="size-3.5" />
        </Button>
        <span className="font-mono text-[11.5px] tabular-nums">
          {currentPage} / {totalPages}
        </span>
        <Button
          variant="outline"
          size="icon"
          className="size-7"
          disabled={offset + pageSize >= total}
          onClick={() => { onChange(offset + pageSize); }}
          aria-label="Next page"
        >
          <ChevronRight className="size-3.5" />
        </Button>
      </div>
    </div>
  );
}

function Th({ children, className }: { children?: React.ReactNode; className?: string }) {
  return (
    <TableHead
      className={cn(
        'h-9 px-3 text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-muted',
        className,
      )}
    >
      {children}
    </TableHead>
  );
}

function Td({ children, className }: { children?: React.ReactNode; className?: string }) {
  return <TableCell className={cn('px-3 py-2.5', className)}>{children}</TableCell>;
}
