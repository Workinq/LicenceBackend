import { createFileRoute, Link } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import type { JSONContent } from '@tiptap/react';
import { ImageOff, ArrowLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { formatPrice } from '@/lib/format';
import { fetchProduct } from '@/api/products';
import { AddToBasketButton } from '@/components/basket/AddToBasketButton';
import { ProductPageContent } from '@/components/products/ProductPageContent';

export const Route = createFileRoute('/portal/products_/$id')({
  component: PortalProductDetailPage,
});

function PortalProductDetailPage() {
  const { id } = Route.useParams();
  const query = useQuery({ queryKey: ['portal', 'products', 'detail', id], queryFn: () => fetchProduct(id) });

  if (query.isPending) return <Skeleton className="h-96 w-full max-w-3xl" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this product.</p>;
  }

  const product = query.data;
  const pageContent = (product.pageContent as JSONContent | null | undefined) ?? null;

  return (
    <div className="max-w-3xl space-y-6">
      <Link to="/portal/products" className="inline-flex items-center gap-1 text-sm text-ink-muted hover:text-ink">
        <ArrowLeft className="size-4" aria-hidden="true" /> Back to products
      </Link>

      {product.imageUrl ? (
        <img src={`/api${product.imageUrl}`} alt="" className="aspect-video w-full rounded-lg object-cover" />
      ) : (
        <div className="flex aspect-video w-full items-center justify-center rounded-lg bg-surface-sunken text-ink-subtle">
          <ImageOff className="size-8" aria-hidden="true" />
        </div>
      )}

      <div className="space-y-2">
        <h1 className="font-display text-2xl font-semibold text-ink">{product.displayName}</h1>
        {product.tagline && <p className="text-ink-muted">{product.tagline}</p>}
        <div className="flex items-center justify-between gap-3">
          <span className="text-lg text-ink">
            {product.price != null ? formatPrice(product.price, product.currency) : 'Free'}
          </span>
          <AddToBasketButton product={product} />
        </div>
      </div>

      {pageContent && <ProductPageContent content={pageContent} />}
    </div>
  );
}
