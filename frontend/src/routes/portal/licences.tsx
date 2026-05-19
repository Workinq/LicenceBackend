import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { fetchMyLicences } from '@/api/me-licences';

export const Route = createFileRoute('/portal/licences')({
  component: PortalLicencesPage,
});

const PAGE_SIZE = 25;

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '-';
}

function PortalLicencesPage() {
  const [offset, setOffset] = useState(0);

  const query = useQuery({
    queryKey: ['portal', 'licences', { offset }],
    queryFn: () => fetchMyLicences({ limit: PAGE_SIZE, offset }),
    placeholderData: keepPreviousData,
  });

  const data = query.data;
  const rangeLabel = data
    ? `${data.total === 0 ? 0 : data.offset + 1}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <h1 className="font-display text-2xl font-semibold text-ink">My licences</h1>

      <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Product</TableHead>
              <TableHead>Label</TableHead>
              <TableHead>Relationship</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>HWID</TableHead>
              <TableHead>Expires</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={6}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {query.isError && (
              <TableRow>
                <TableCell colSpan={6} className="text-sm text-status-revoked-fg">
                  Failed to load your licences.
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((lic) => (
              <TableRow key={lic.id}>
                <TableCell>
                  <Link
                    to="/portal/licences/$id"
                    params={{ id: lic.id }}
                    className="font-medium text-ink underline-offset-2 hover:underline"
                  >
                    {lic.productSlug}
                  </Link>
                </TableCell>
                <TableCell className="text-ink-muted">{lic.label ?? <span className="text-ink-subtle">-</span>}</TableCell>
                <TableCell>
                  <Badge variant={lic.relationship === 'owner' ? 'default' : 'secondary'} className="capitalize">
                    {lic.relationship ?? 'owner'}
                  </Badge>
                </TableCell>
                <TableCell><StatusPill status={lic.status} /></TableCell>
                <TableCell className="text-ink-muted">{lic.hwidBound ? 'Bound' : '-'}</TableCell>
                <TableCell className="text-ink-muted">{formatDate(lic.expiresAt)}</TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={6} className="text-sm text-ink-muted">
                  You do not have any licences yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <div className="flex items-center justify-between text-sm text-ink-muted">
        <span>{rangeLabel}</span>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={offset === 0}
            onClick={() => setOffset(Math.max(0, offset - PAGE_SIZE))}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!data || offset + PAGE_SIZE >= data.total}
            onClick={() => setOffset(offset + PAGE_SIZE)}
          >
            Next
          </Button>
        </div>
      </div>
    </div>
  );
}
