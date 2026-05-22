import { useEffect, useState } from 'react';
import { createFileRoute, Link, useBlocker } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import type { JSONContent } from '@tiptap/react';
import { ArrowLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { fetchProduct } from '@/api/products';
import { ProductPageEditor } from '@/components/products/ProductPageEditor';

export const Route = createFileRoute('/admin/products_/$id/page')({
  component: ProductPageEditRoute,
});

function ProductPageEditRoute() {
  const { id } = Route.useParams();
  const query = useQuery({ queryKey: ['products', 'detail', id], queryFn: () => fetchProduct(id) });
  const [dirty, setDirty] = useState(false);

  useEffect(() => {
    if (!dirty) return;
    const handler = (e: BeforeUnloadEvent) => { e.preventDefault(); };
    window.addEventListener('beforeunload', handler);
    return () => { window.removeEventListener('beforeunload', handler); };
  }, [dirty]);

  useBlocker({
    shouldBlockFn: () => {
      if (!dirty) return false;
      return !window.confirm('You have unsaved changes. Leave this page anyway?');
    },
  });

  if (query.isPending) return <Skeleton className="h-screen w-full" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this product.</p>;
  }

  const product = query.data;

  return (
    <div className="space-y-4">
      <div className="space-y-1">
        <Link
          to="/admin/products/$id"
          params={{ id }}
          className="inline-flex items-center gap-1 text-sm text-ink-muted hover:text-ink"
        >
          <ArrowLeft className="size-4" aria-hidden="true" /> Back to product
        </Link>
        <h1 className="font-display text-2xl font-semibold text-ink">
          {product.displayName} - page content
        </h1>
      </div>

      <ProductPageEditor
        productId={id}
        initialContent={(product.pageContent as JSONContent | null | undefined) ?? null}
        onDirtyChange={setDirty}
      />
    </div>
  );
}
