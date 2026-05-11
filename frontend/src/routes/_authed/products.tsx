import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { buttonVariants } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { fetchProducts } from '@/api/products';

export const Route = createFileRoute('/_authed/products')({
  component: ProductsPage,
});

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

function ProductsPage() {
  const query = useQuery({ queryKey: ['products'], queryFn: fetchProducts });
  const items = query.data?.items ?? [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
        <Link to="/products/new" className={buttonVariants()}>
          New product
        </Link>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Slug</TableHead>
              <TableHead>Display name</TableHead>
              <TableHead>Created</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {query.isPending && (
              <TableRow>
                <TableCell colSpan={3}>
                  <Skeleton className="h-6 w-full" />
                </TableCell>
              </TableRow>
            )}
            {query.isError && (
              <TableRow>
                <TableCell colSpan={3} className="text-sm text-status-revoked-fg">
                  Failed to load products.
                </TableCell>
              </TableRow>
            )}
            {items.map((p) => (
              <TableRow key={p.id}>
                <TableCell className="font-mono text-sm text-ink">{p.slug}</TableCell>
                <TableCell className="text-ink">{p.displayName}</TableCell>
                <TableCell className="text-ink-muted">{formatDate(p.createdAt)}</TableCell>
              </TableRow>
            ))}
            {query.data && items.length === 0 && !query.isError && (
              <TableRow>
                <TableCell colSpan={3} className="text-sm text-ink-muted">
                  No products yet. Create one to get started.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
