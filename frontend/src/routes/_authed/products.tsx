import { useState } from 'react';
import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { buttonVariants } from '@/components/ui/button';
import { ImageOff } from 'lucide-react';
import { fetchProducts } from '@/api/products';

export const Route = createFileRoute('/_authed/products')({
  component: ProductsPage,
});

function formatPrice(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

function ProductsPage() {
  const query = useQuery({ queryKey: ['products'], queryFn: fetchProducts });
  const [search, setSearch] = useState('');
  const allItems = query.data?.items ?? [];
  const items = allItems.filter((p) =>
    p.displayName.toLowerCase().includes(search.trim().toLowerCase()),
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
        <Link to="/products/new" className={buttonVariants()}>
          New product
        </Link>
      </div>

      <Input
        type="search"
        placeholder="Search products by name"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="max-w-sm"
      />

      {query.isPending && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <Card key={i} className="overflow-hidden">
              <Skeleton className="aspect-video w-full" />
              <CardHeader>
                <Skeleton className="h-5 w-2/3" />
              </CardHeader>
            </Card>
          ))}
        </div>
      )}

      {query.isError && (
        <p className="text-sm text-status-revoked-fg">Failed to load products.</p>
      )}

      {query.data && (
        <>
          {allItems.length === 0 && (
            <p className="text-sm text-ink-muted">No products yet. Create one to get started.</p>
          )}
          {allItems.length > 0 && items.length === 0 && (
            <p className="text-sm text-ink-muted">No products match your search.</p>
          )}
          {items.length > 0 && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {items.map((p) => (
                <Link
                  key={p.id}
                  to="/products/$id"
                  params={{ id: p.id }}
                  className="block rounded-lg transition-shadow hover:shadow-card"
                >
                  <Card className="overflow-hidden">
                    {p.imageUrl ? (
                      <img
                        src={`/api${p.imageUrl}`}
                        alt=""
                        className="aspect-video w-full object-cover"
                      />
                    ) : (
                      <div className="flex aspect-video w-full items-center justify-center bg-surface-sunken text-ink-subtle">
                        <ImageOff className="size-6" aria-hidden="true" />
                      </div>
                    )}
                    <CardHeader>
                      <div className="flex items-start justify-between gap-2">
                        <CardTitle className="truncate">{p.displayName}</CardTitle>
                        <Badge variant={p.isPublic ? 'default' : 'secondary'}>
                          {p.isPublic ? 'Public' : 'Private'}
                        </Badge>
                      </div>
                      <CardDescription className="font-mono text-xs">{p.slug}</CardDescription>
                      {p.tagline && <p className="text-sm text-ink-muted">{p.tagline}</p>}
                    </CardHeader>
                    <CardContent className="text-sm">
                      {p.price != null ? (
                        formatPrice(p.price, p.currency)
                      ) : (
                        <span className="text-ink-subtle">No price</span>
                      )}
                    </CardContent>
                  </Card>
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
