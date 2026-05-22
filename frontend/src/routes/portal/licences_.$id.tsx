import { useState } from 'react';
import { createFileRoute, Link, useNavigate } from '@tanstack/react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { StatusPill } from '@/components/StatusPill';
import { ConfirmDestructive } from '@/components/ConfirmDestructive';
import {
  addMyLicenceMember,
  downloadMyLicenceFile,
  fetchMyLicence,
  fetchMyLicenceMembers,
  regenerateMyLicenceKey,
  removeMyLicenceMember,
} from '@/api/me-licences';
import { triggerBlobDownload } from '@/api/product-files';
import { RegenerateKeyDialog } from '@/components/licences/RegenerateKeyDialog';
import { LicenceLabelEditor } from '@/components/licences/LicenceLabelEditor';
import { PortalLicenceSessions } from '@/components/licences/PortalLicenceSessions';
import { ApiError } from '@/auth/api-client';

export const Route = createFileRoute('/portal/licences_/$id')({
  component: PortalLicenceDetail,
});

function formatDateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString() : 'Never';
}

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function errorDetail(error: unknown, fallback: string): string {
  if (error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body) {
    return String((error.body as Record<string, unknown>).detail);
  }
  return fallback;
}

function DownloadLatestButton({ licenceId, productSlug }: { licenceId: string; productSlug: string }) {
  const [pending, setPending] = useState(false);

  const onClick = async () => {
    setPending(true);
    try {
      const file = await downloadMyLicenceFile(licenceId);
      triggerBlobDownload(file, `${productSlug}-latest`);
    } catch (error) {
      const detail =
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : null;
      if (error instanceof ApiError && error.status === 404) {
        toast.info('No download is available for this product yet.');
      } else {
        toast.error(detail ?? 'Could not download the latest file.');
      }
    } finally {
      setPending(false);
    }
  };

  return (
    <Button type="button" onClick={() => { void onClick(); }} disabled={pending}>
      <Download className="size-4" aria-hidden="true" />
      <span className="ml-1.5">{pending ? 'Downloading...' : 'Download latest'}</span>
    </Button>
  );
}

function PortalLicenceDetail() {
  const { id } = Route.useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [memberEmail, setMemberEmail] = useState('');
  const [memberError, setMemberError] = useState<string | null>(null);

  const query = useQuery({ queryKey: ['portal', 'licences', 'detail', id], queryFn: () => fetchMyLicence(id) });

  const membersQuery = useQuery({
    queryKey: ['portal', 'licences', 'members', id],
    queryFn: () => fetchMyLicenceMembers(id),
    enabled: query.data?.relationship === 'owner',
  });

  const addMember = useMutation({
    mutationFn: (email: string) => addMyLicenceMember(id, { email }),
    onSuccess: () => {
      setMemberEmail('');
      setMemberError(null);
      void queryClient.invalidateQueries({ queryKey: ['portal', 'licences', 'members', id] });
    },
    onError: (error: unknown) => {
      setMemberError(errorDetail(error, 'Could not add the member.'));
    },
  });

  const removeMember = useMutation({
    mutationFn: (memberId: string) => removeMyLicenceMember(id, memberId),
    onSuccess: () => {
      setMemberError(null);
      void queryClient.invalidateQueries({ queryKey: ['portal', 'licences', 'members', id] });
    },
    onError: (error: unknown) => {
      setMemberError(errorDetail(error, 'Could not remove the member.'));
    },
  });

  if (query.isPending) return <Skeleton className="h-64 w-full max-w-3xl" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this licence.</p>;
  }

  const lic = query.data;
  const isOwner = lic.relationship === 'owner';

  return (
    <div className="max-w-3xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" onClick={() => { void navigate({ to: '/portal/licences' }); }}>
          {'< Back to my licences'}
        </Button>
      </div>

      <div className="flex items-center gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">{lic.productSlug}</h1>
        <StatusPill status={lic.status} />
        {lic.orderId && (
          <Button asChild variant="outline" size="sm" className="ml-auto">
            <Link to="/portal/orders/$id" params={{ id: lic.orderId }}>
              View order
            </Link>
          </Button>
        )}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[10rem_1fr] items-baseline gap-y-3 text-sm">
            <dt className="text-ink-muted">Product</dt>
            <dd>
              <Link
                to="/portal/products"
                className="text-ink underline-offset-2 hover:underline"
              >
                {lic.productSlug}
              </Link>
            </dd>
            <dt className="text-ink-muted">Label</dt>
            <dd><LicenceLabelEditor licenceId={lic.id} label={lic.label} editable={isOwner} /></dd>
            <dt className="text-ink-muted">Relationship</dt>
            <dd className="capitalize text-ink">{lic.relationship ?? 'owner'}</dd>
            <dt className="text-ink-muted">Owner</dt>
            <dd className="text-ink">{lic.userEmail}</dd>
            <dt className="text-ink-muted">HWID</dt>
            <dd className="text-ink">{lic.hwidBound ? 'Bound' : 'Not bound'}</dd>
            <dt className="text-ink-muted">IP allowlist</dt>
            <dd className="text-ink">
              {lic.ipAllowlist == null
                ? 'None'
                : lic.ipAllowlist.length === 0
                ? 'Armed (binds the first verifying IP)'
                : lic.ipAllowlist.join(', ')}
            </dd>
            <dt className="text-ink-muted">Expires</dt>
            <dd className="text-ink">{formatDateTime(lic.expiresAt)}</dd>
            <dt className="text-ink-muted">Notes</dt>
            <dd className="whitespace-pre-wrap text-ink">{lic.notes ?? 'None'}</dd>
          </dl>
        </CardContent>
      </Card>

      {lic.status === 'active' && (
        <Card>
          <CardHeader>
            <CardTitle>Download</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-ink-muted">
              Download the latest release of {lic.productSlug}. Updates are pushed by the publisher.
            </p>
            <DownloadLatestButton licenceId={lic.id} productSlug={lic.productSlug} />
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Licence key</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-ink-muted">
            The licence key is shown only once at creation. Only the licence owner can perform actions on it.
          </p>
          {isOwner && lic.status === 'active' && (
            <RegenerateKeyDialog
              licenceId={lic.id}
              regenerate={(id) => regenerateMyLicenceKey(id, { reason: null })}
              invalidateQueryKey={['portal', 'licences', 'detail', lic.id]}
            />
          )}
          {isOwner && lic.status !== 'active' && (
            <span title={`The key cannot be regenerated for a ${lic.status} licence.`} className="inline-block">
              <Button variant="outline" disabled className="pointer-events-none">
                Regenerate key
              </Button>
            </span>
          )}
          {!isOwner && (
            <span title="Only the owner can regenerate the key." className="inline-block">
              <Button variant="outline" disabled className="pointer-events-none">
                Regenerate key
              </Button>
            </span>
          )}
        </CardContent>
      </Card>

      {isOwner && (
        <Card>
          <CardHeader>
            <CardTitle>Members</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              onSubmit={(e) => {
                e.preventDefault();
                setMemberError(null);
                const trimmed = memberEmail.trim();
                if (trimmed) addMember.mutate(trimmed);
              }}
              className="space-y-2"
              noValidate
            >
              <Label htmlFor="member-email">Add member by email</Label>
              <div className="flex gap-2">
                <Input
                  id="member-email"
                  type="email"
                  placeholder="user@example.com"
                  value={memberEmail}
                  onChange={(e) => { setMemberEmail(e.target.value); }}
                  disabled={addMember.isPending}
                  className="max-w-sm"
                />
                <Button type="submit" disabled={addMember.isPending || memberEmail.trim().length === 0}>
                  Add
                </Button>
              </div>
              {memberError && (
                <Alert variant="destructive">
                  <AlertDescription>{memberError}</AlertDescription>
                </Alert>
              )}
            </form>

            {membersQuery.isPending && <Skeleton className="h-10 w-full" />}
            {membersQuery.isError && (
              <p className="text-sm text-status-revoked-fg">Failed to load members.</p>
            )}
            {membersQuery.data && membersQuery.data.length === 0 && (
              <p className="text-sm text-ink-muted">No members yet.</p>
            )}
            {membersQuery.data && membersQuery.data.length > 0 && (
              <ul className="divide-y divide-border rounded-lg border border-border">
                {membersQuery.data.map((m) => (
                  <li key={m.userId} className="flex items-center justify-between gap-3 px-4 py-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-ink">{m.email}</p>
                      <p className="text-xs text-ink-muted">Added {formatDate(m.addedAt)}</p>
                    </div>
                    <ConfirmDestructive
                      trigger={
                        <Button variant="ghost" size="icon" disabled={removeMember.isPending} aria-label={`Remove ${m.email}`}>
                          <Trash2 className="size-4" />
                        </Button>
                      }
                      title="Remove member"
                      description={`Remove ${m.email} from this licence?`}
                      confirmLabel="Remove"
                      onConfirm={() => { removeMember.mutate(m.userId); }}
                    />
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Active sessions</CardTitle>
        </CardHeader>
        <CardContent>
          <PortalLicenceSessions licenceId={lic.id} />
        </CardContent>
      </Card>
    </div>
  );
}
