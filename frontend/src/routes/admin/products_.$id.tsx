import { useId, useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Download, ImageOff, Upload } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CurrencyCombobox } from '@/components/CurrencyCombobox';
import { Button, buttonVariants } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ImagePickerButton } from '@/components/ImagePickerButton';
import { fetchProduct, updateProduct, uploadProductImage, deleteProductImage } from '@/api/products';
import { downloadProductFileRevision, fetchProductFiles, triggerBlobDownload, uploadProductFile } from '@/api/product-files';
import { ApiError } from '@/auth/api-client';
import { cn } from '@/lib/utils';
import { LicenceKey } from '@/components/LicenceKey';
import type { ProductFileResponse, ProductResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/admin/products_/$id')({
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

      <ProductDownloadsCard productId={id} />

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="mb-4 space-y-1">
            <Label>ID</Label>
            <div><LicenceKey value={product.id} /></div>
          </div>

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
              <Button type="button" variant="outline" onClick={() => { void navigate({ to: '/admin/products' }); }}>
                Cancel
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

function ProductDownloadsCard({ productId }: { productId: string }) {
  const queryClient = useQueryClient();
  const filesQuery = useQuery({
    queryKey: ['products', 'files', productId],
    queryFn: () => fetchProductFiles(productId),
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadProductFile(productId, file),
    onSuccess: (uploaded) => {
      void queryClient.invalidateQueries({ queryKey: ['products', 'files', productId] });
      toast.success(`Uploaded version ${uploaded.versionNumber}.`);
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the file.'));
    },
  });

  const inputId = useId();

  const downloadRevision = async (file: ProductFileResponse) => {
    try {
      const blob = await downloadProductFileRevision(productId, file.id);
      triggerBlobDownload(blob, file.fileName);
    } catch (error) {
      toast.error(errorDetail(error, 'Could not download this revision.'));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Downloads</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-ink-muted">
          Users see only the most recent revision. Older revisions remain here for your reference.
        </p>
        <div>
          <label
            htmlFor={inputId}
            className={cn(
              buttonVariants({ variant: 'outline', size: 'sm' }),
              'cursor-pointer',
              uploadMutation.isPending && 'pointer-events-none opacity-50',
            )}
          >
            <Upload className="size-4" aria-hidden="true" />
            <span className="ml-1.5">{filesQuery.data && filesQuery.data.length > 0 ? 'Upload new revision' : 'Upload first revision'}</span>
          </label>
          <input
            id={inputId}
            type="file"
            className="sr-only"
            disabled={uploadMutation.isPending}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) uploadMutation.mutate(f);
              e.target.value = '';
            }}
          />
        </div>

        {filesQuery.isPending && <Skeleton className="h-16 w-full" />}
        {filesQuery.isError && (
          <p className="text-sm text-status-revoked-fg">Failed to load revisions.</p>
        )}
        {filesQuery.data && filesQuery.data.length === 0 && (
          <p className="text-sm text-ink-muted">No revisions uploaded yet.</p>
        )}
        {filesQuery.data && filesQuery.data.length > 0 && (
          <ul className="divide-y divide-border rounded-lg border border-border">
            {filesQuery.data.map((f, idx) => (
              <li key={f.id} className="flex items-center justify-between gap-3 px-4 py-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-ink">
                    v{f.versionNumber}
                    {idx === 0 && <span className="ml-2 rounded-full bg-surface-sunken px-2 py-0.5 text-xs font-normal text-ink-muted">latest</span>}
                  </p>
                  <p className="truncate text-xs text-ink-muted">
                    {f.fileName} | {formatFileSize(f.fileSizeBytes)} | {new Date(f.uploadedAt).toLocaleString()}
                  </p>
                </div>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => { void downloadRevision(f); }}
                  aria-label={`Download v${f.versionNumber}`}
                >
                  <Download className="size-4" aria-hidden="true" />
                </Button>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
