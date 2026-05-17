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
import { Button, buttonVariants } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { StatusPill } from '@/components/StatusPill';
import { fetchUsers } from '@/api/users';

export const Route = createFileRoute('/admin/users')({
  component: UsersPage,
});

const PAGE_SIZE = 25;
const ROLE_FILTERS = ['all', 'admin', 'user'] as const;
const STATUS_FILTERS = ['all', 'active', 'suspended'] as const;
type RoleFilter = (typeof ROLE_FILTERS)[number];
type StatusFilter = (typeof STATUS_FILTERS)[number];

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function UsersPage() {
  const [search, setSearch] = useState('');
  const [role, setRole] = useState<RoleFilter>('all');
  const [status, setStatus] = useState<StatusFilter>('all');
  const [offset, setOffset] = useState(0);

  const trimmed = search.trim();
  const query = useQuery({
    queryKey: ['users', 'list', { q: trimmed, role, status, offset }],
    queryFn: () =>
      fetchUsers({
        q: trimmed || undefined,
        role: role === 'all' ? undefined : role,
        status: status === 'all' ? undefined : status,
        limit: PAGE_SIZE,
        offset,
      }),
    placeholderData: keepPreviousData,
  });

  const onSearchChange = (next: string) => {
    setSearch(next);
    setOffset(0);
  };
  const onRoleChange = (next: string) => {
    setRole(next as RoleFilter);
    setOffset(0);
  };
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
        <h1 className="font-display text-2xl font-semibold text-ink">Users</h1>
        <Link to="/admin/users/new" className={buttonVariants()}>
          New user
        </Link>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search"
          placeholder="Search by email"
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          className="max-w-xs"
        />
        <Select value={role} onValueChange={onRoleChange}>
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {ROLE_FILTERS.map((r) => (
              <SelectItem key={r} value={r} className="capitalize">
                {r === 'all' ? 'All roles' : r}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
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
              <TableHead>Email</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Role</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Created</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {query.isError && (
              <TableRow>
                <TableCell colSpan={5} className="text-sm text-status-revoked-fg">
                  Failed to load users.
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((u) => (
              <TableRow key={u.id}>
                <TableCell>
                  <Link
                    to="/admin/users/$id"
                    params={{ id: u.id }}
                    className="font-medium text-ink underline-offset-2 hover:underline"
                  >
                    {u.email}
                  </Link>
                </TableCell>
                <TableCell className="text-ink-muted">{u.displayName ?? '-'}</TableCell>
                <TableCell className="capitalize text-ink-muted">{u.role}</TableCell>
                <TableCell>
                  <StatusPill status={u.status} />
                </TableCell>
                <TableCell className="text-ink-muted">{formatDate(u.createdAt)}</TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={5} className="text-sm text-ink-muted">
                  No users match these filters.
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
