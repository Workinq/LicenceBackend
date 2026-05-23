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
import { cn } from '@/lib/utils';

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
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeLabel = data
    ? `${rangeStart}-${Math.min(data.offset + data.limit, data.total)} of ${data.total}`
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

      <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
        <Table className="text-[12.5px]">
          <TableHeader>
            <TableRow className="border-border">
              <Th>Email</Th>
              <Th>Name</Th>
              <Th className="w-[80px]">Role</Th>
              <Th className="w-[100px]">Status</Th>
              <Th className="w-[110px]">Created</Th>
              <Th className="w-[80px]" />
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
                <TableCell colSpan={6} className="text-status-revoked-fg">
                  Failed to load users.
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((u) => (
              <TableRow key={u.id} className="border-border hover:bg-surface-sunken">
                <Td className="font-medium text-foreground">{u.email}</Td>
                <Td className="text-ink-muted">{u.displayName ?? '-'}</Td>
                <Td className="capitalize text-ink-muted">{u.role}</Td>
                <Td>
                  <StatusPill status={u.status} />
                </Td>
                <Td className="font-mono text-[11.5px] text-ink-muted">{formatDate(u.createdAt)}</Td>
                <Td>
                  <Link
                    to="/admin/users/$id"
                    params={{ id: u.id }}
                    className="text-[12px] text-accent hover:underline"
                  >
                    View
                  </Link>
                </Td>
              </TableRow>
            ))}
            {data?.items.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={6} className="text-ink-muted">
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

function Th({ children, className }: Readonly<{ children?: React.ReactNode; className?: string }>) {
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

function Td({ children, className }: Readonly<{ children?: React.ReactNode; className?: string }>) {
  return <TableCell className={cn('px-3 py-2.5', className)}>{children}</TableCell>;
}
