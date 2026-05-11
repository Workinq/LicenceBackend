import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/_authed/products')({
  component: ProductsPage,
});

function ProductsPage() {
  return (
    <div>
      <h1 className="font-display text-2xl font-semibold text-ink">Products</h1>
      <p className="mt-2 text-sm text-ink-subtle">Coming in Chunk P1d.</p>
    </div>
  );
}
