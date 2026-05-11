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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { fetchLicences } from '@/api/licences';

export const Route = createFileRoute('/_authed/licences')({
  component: LicencesPage,
});

const PAGE_SIZE = 25;
const STATUS_FILTERS = ['all', 'active', 'suspended', 'revoked'] as const;
type StatusFilter = (typeof STATUS_FILTERS)[number];

function formatDate(value: string | null): string {
  return value ? new Date(value).toLocaleDateString() : '-';
}

function LicencesPage() {
  const [status, setStatus] = useState<StatusFilter>('all');
  const [offset, setOffset] = useState(0);

  const query = useQuery({
    queryKey: ['licences', 'list', { status, offset }],
    queryFn: () =>
      fetchLicences({
        status: status === 'all' ? undefined : status,
        limit: PAGE_SIZE,
        offset,
      }),
    placeholderData: keepPreviousData,
  });

  const onStatusChange = (next: string) => {
    setStatus(next as StatusFilter);
    setOffset(0);
  };

  const data = query.data;
  const rangeLabel = data
    ? `${data.total === 0 ? 0 : data.offset + 1}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
    : '';

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-ink">Licences</h1>
        <Button asChild>
          <Link to="/licences/new">New licence</Link>
        </Button>
      </div>

      <div className="flex items-center gap-3">
        <Select value={status} onValueChange={onStatusChange}>
          <SelectTrigger className="w-44">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {STATUS_FILTERS.map((s) => (
              <SelectItem key={s} value={s} className="capitalize">
                {s === 'all' ? 'All statuses' : s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Product</TableHead>
              <TableHead>User</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>HWID</TableHead>
              <TableHead>Expires</TableHead>
              <TableHead>Created</TableHead>
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
                  Failed to load licences.
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((lic) => (
              <TableRow key={lic.id}>
                <TableCell>
                  <Link
                    to="/licences/$id"
                    params={{ id: lic.id }}
                    className="font-medium text-ink underline-offset-2 hover:underline"
                  >
                    {lic.productSlug}
                  </Link>
                </TableCell>
                <TableCell className="text-ink-muted">{lic.userEmail}</TableCell>
                <TableCell>
                  <StatusPill status={lic.status} />
                </TableCell>
                <TableCell className="text-ink-muted">{lic.hwidBound ? 'Bound' : '-'}</TableCell>
                <TableCell className="text-ink-muted">{formatDate(lic.expiresAt)}</TableCell>
                <TableCell className="text-ink-muted">{formatDate(lic.createdAt)}</TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={6} className="text-sm text-ink-muted">
                  No licences match this filter.
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
