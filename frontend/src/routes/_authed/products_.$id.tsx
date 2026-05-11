import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { ImageOff } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CurrencyCombobox } from '@/components/CurrencyCombobox';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ImagePickerButton } from '@/components/ImagePickerButton';
import { fetchProduct, updateProduct, uploadProductImage, deleteProductImage } from '@/api/products';
import { ApiError } from '@/auth/api-client';
import type { ProductResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/_authed/products_/$id')({
  component: ProductDetailPage,
});

const schema = z.object({
  displayName: z.string().min(1, 'Display name is required'),
  description: z.string().optional(),
  tagline: z.string().optional(),
  isPublic: z.boolean(),
  price: z.string().optional(),
  currency: z.string().regex(/^[A-Z]{3}$/, 'Three uppercase letters').optional().or(z.literal('')),
  sortOrder: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function errorDetail(error: unknown, fallback: string): string {
  return error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
    ? String((error.body as Record<string, unknown>).detail)
    : fallback;
}

function ProductDetailPage() {
  const { id } = Route.useParams();
  const query = useQuery({ queryKey: ['products', 'detail', id], queryFn: () => fetchProduct(id) });

  if (query.isPending) return <Skeleton className="h-96 w-full max-w-2xl" />;
  if (query.isError || !query.data) {
    return <p className="text-sm text-status-revoked-fg">Failed to load this product.</p>;
  }
  return <ProductDetailContent product={query.data} />;
}

function ProductDetailContent({ product }: { product: ProductResponse }) {
  const id = product.id;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [imageVersion, setImageVersion] = useState(0);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const editMutation = useMutation({
    mutationFn: (values: FormValues) =>
      updateProduct(id, {
        displayName: values.displayName,
        description: values.description ? values.description : null,
        tagline: values.tagline ? values.tagline : null,
        isPublic: values.isPublic,
        price: values.price ? Number(values.price) : null,
        currency: values.currency ? values.currency : null,
        sortOrder: values.sortOrder ? Number(values.sortOrder) : null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] });
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      toast.success('Product updated.');
    },
    onError: (error) => {
      setSubmitError(errorDetail(error, 'Could not update the product.'));
    },
  });

  const imageMutation = useMutation({
    mutationFn: (f: File) => uploadProductImage(id, f),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] });
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      setImageVersion((v) => v + 1);
      toast.success('Image uploaded.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the image.'));
    },
  });

  const imageDeleteMutation = useMutation({
    mutationFn: () => deleteProductImage(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] });
      void queryClient.invalidateQueries({ queryKey: ['products'] });
      setImageVersion((v) => v + 1);
      toast.success('Image removed.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not remove the image.'));
    },
  });

  const { register, control, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      displayName: product.displayName,
      description: product.description ?? '',
      tagline: product.tagline ?? '',
      isPublic: product.isPublic,
      price: product.price != null ? String(product.price) : '',
      currency: product.currency,
      sortOrder: String(product.sortOrder),
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSubmitError(null);
    await editMutation.mutateAsync(values);
  };

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">{product.displayName}</h1>

      <Card>
        <CardHeader>
          <CardTitle>Image</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {product.imageUrl ? (
            <img
              src={`/api${product.imageUrl}?v=${imageVersion}`}
              alt=""
              className="aspect-video w-full max-w-md rounded-lg object-cover"
            />
          ) : (
            <div className="flex aspect-video w-full max-w-md items-center justify-center rounded-lg bg-surface-sunken text-ink-subtle">
              <ImageOff className="size-8" aria-hidden="true" />
            </div>
          )}
          <div className="flex items-center gap-3">
            <ImagePickerButton
              label={product.imageUrl ? 'Replace image' : 'Upload image'}
              onSelect={(f) => { imageMutation.mutate(f); }}
              disabled={imageMutation.isPending}
            />
            {product.imageUrl && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => { imageDeleteMutation.mutate(); }}
                disabled={imageDeleteMutation.isPending}
              >
                Remove image
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="mb-4 space-y-1">
            <Label htmlFor="slug">Slug</Label>
            <Input id="slug" value={product.slug} disabled readOnly className="font-mono" />
            <p className="text-xs text-ink-subtle">Slugs cannot be changed.</p>
          </div>

          <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
            {submitError && (
              <Alert variant="destructive">
                <AlertDescription>{submitError}</AlertDescription>
              </Alert>
            )}

            <div className="space-y-1">
              <Label htmlFor="displayName">Display name</Label>
              <Input id="displayName" {...register('displayName')} />
              {errors.displayName && (
                <p className="text-xs text-status-revoked-fg">{errors.displayName.message}</p>
              )}
            </div>

            <div className="space-y-1">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" {...register('description')} />
            </div>

            <div className="space-y-1">
              <Label htmlFor="tagline">Tagline</Label>
              <Input id="tagline" {...register('tagline')} />
            </div>

            <div className="flex items-center gap-2">
              <Controller
                name="isPublic"
                control={control}
                render={({ field }) => (
                  <Switch id="isPublic" checked={field.value} onCheckedChange={field.onChange} />
                )}
              />
              <Label htmlFor="isPublic">Public</Label>
            </div>

            <div className="space-y-1">
              <Label htmlFor="price">Price</Label>
              <Input id="price" type="number" step="0.01" min="0" {...register('price')} />
            </div>

            <div className="space-y-1">
              <Label htmlFor="currency">Currency</Label>
              <Controller
                name="currency"
                control={control}
                render={({ field }) => (
                  <CurrencyCombobox id="currency" value={field.value ?? ''} onChange={field.onChange} />
                )}
              />
              {errors.currency && (
                <p className="text-xs text-status-revoked-fg">{errors.currency.message}</p>
              )}
            </div>

            <div className="space-y-1">
              <Label htmlFor="sortOrder">Sort order</Label>
              <Input id="sortOrder" type="number" {...register('sortOrder')} />
            </div>

            <div className="flex gap-3">
              <Button type="submit" disabled={isSubmitting || editMutation.isPending}>
                Save changes
              </Button>
              <Button type="button" variant="outline" onClick={() => { void navigate({ to: '/products' }); }}>
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
