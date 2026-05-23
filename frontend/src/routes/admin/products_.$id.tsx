import { useId, useState } from 'react';
import { createFileRoute, useNavigate, Link } from '@tanstack/react-router';
import { keepPreviousData, useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import {
  ChevronLeft,
  ChevronRight,
  Download,
  FileText,
  ImageOff,
  Plus,
  Upload,
} from 'lucide-react';
import { CurrencyCombobox } from '@/components/CurrencyCombobox';
import { Button, buttonVariants } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ImagePickerButton } from '@/components/ImagePickerButton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { StatusPill } from '@/components/StatusPill';
import { KeyChip } from '@/components/dashboard/KeyChip';
import { fetchProduct, updateProduct, uploadProductImage, deleteProductImage } from '@/api/products';
import {
  downloadProductFileRevision,
  fetchProductFiles,
  triggerBlobDownload,
  uploadProductFile,
} from '@/api/product-files';
import { fetchLicences } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import { cn } from '@/lib/utils';
import { formatDate, formatRelative } from '@/lib/format';
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

  if (query.isPending) return <Skeleton className="h-64 w-full" />;
  if (query.isError || !query.data) {
    return <p className="text-[12.5px] text-status-revoked-fg">Failed to load this product.</p>;
  }
  return <ProductDetailContent product={query.data} />;
}

function ProductDetailContent({ product }: Readonly<{ product: ProductResponse }>) {
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
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] }),
        queryClient.invalidateQueries({ queryKey: ['products'] }),
      ]);
      toast.success('Product updated.');
    },
    onError: (error) => {
      setSubmitError(errorDetail(error, 'Could not update the product.'));
    },
  });

  const imageMutation = useMutation({
    mutationFn: (f: File) => uploadProductImage(id, f),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] }),
        queryClient.invalidateQueries({ queryKey: ['products'] }),
      ]);
      setImageVersion((v) => v + 1);
      toast.success('Image uploaded.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the image.'));
    },
  });

  const imageDeleteMutation = useMutation({
    mutationFn: () => deleteProductImage(id),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['products', 'detail', id] }),
        queryClient.invalidateQueries({ queryKey: ['products'] }),
      ]);
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
      price: product.price == null ? '' : String(product.price),
      currency: product.currency,
      sortOrder: String(product.sortOrder),
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSubmitError(null);
    await editMutation.mutateAsync(values);
  };

  const filesQuery = useQuery({
    queryKey: ['products', 'files', id],
    queryFn: () => fetchProductFiles(id),
  });
  const licenceCountQuery = useQuery({
    queryKey: ['licences', 'count', { productId: id }],
    queryFn: () => fetchLicences({ productId: id, limit: 1, offset: 0 }),
    staleTime: 30_000,
  });
  const activeLicenceCountQuery = useQuery({
    queryKey: ['licences', 'count', { productId: id, status: 'active' }],
    queryFn: () => fetchLicences({ productId: id, status: 'active', limit: 1, offset: 0 }),
    staleTime: 30_000,
  });

  const revisionCount = filesQuery.data?.length ?? 0;
  const lastUploadAt = filesQuery.data?.[0]?.uploadedAt;

  return (
    <div className="space-y-5">
      <header className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-[20px] font-semibold tracking-tight text-foreground">{product.displayName}</h1>
          <KeyChip value={product.slug} />
          <StatusPill status={product.isPublic ? 'active' : 'private'} />
          <div className="ml-auto flex items-center gap-2">
            <Link
              to="/admin/products/$id/page"
              params={{ id }}
              className={cn(buttonVariants({ variant: 'outline', size: 'sm' }), 'gap-1.5')}
            >
              <FileText className="size-3.5" aria-hidden />
              Edit product page
            </Link>
          </div>
        </div>
        <p className="text-[12px] text-ink-muted">
          Product ID <KeyChip value={product.id} display={product.id.slice(0, 20)} className="ml-1" />
        </p>
      </header>

      <div className="grid grid-cols-2 gap-px overflow-hidden rounded-md border border-border bg-border text-[12.5px] sm:grid-cols-3 lg:grid-cols-5">
        <StatCell label="Active licences" value={activeLicenceCountQuery.data?.total?.toLocaleString() ?? '-'} />
        <StatCell label="Total licences" value={licenceCountQuery.data?.total?.toLocaleString() ?? '-'} />
        <StatCell label="Revisions" value={String(revisionCount)} />
        <StatCell label="Last upload" value={lastUploadAt ? formatRelative(lastUploadAt) : 'Never'} />
        <StatCell label="Created" value={formatDate(product.createdAt)} />
      </div>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-[1fr_320px]">
        <DetailCard title="Details">
          <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4 text-[12.5px]">
            {submitError && (
              <Alert variant="destructive">
                <AlertDescription>{submitError}</AlertDescription>
              </Alert>
            )}

            <Field label="Display name" htmlFor="displayName" error={errors.displayName?.message}>
              <Input id="displayName" {...register('displayName')} />
            </Field>

            <Field label="Tagline" htmlFor="tagline">
              <Input id="tagline" {...register('tagline')} />
            </Field>

            <Field label="Description" htmlFor="description">
              <Textarea id="description" rows={4} {...register('description')} />
            </Field>

            <div className="grid grid-cols-2 gap-3">
              <Field label="Price" htmlFor="price">
                <Input id="price" type="number" step="0.01" min="0" {...register('price')} />
              </Field>
              <Field label="Currency" htmlFor="currency" error={errors.currency?.message}>
                <Controller
                  name="currency"
                  control={control}
                  render={({ field }) => (
                    <CurrencyCombobox id="currency" value={field.value ?? ''} onChange={field.onChange} />
                  )}
                />
              </Field>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <Field label="Sort order" htmlFor="sortOrder">
                <Input id="sortOrder" type="number" {...register('sortOrder')} />
              </Field>
              <div className="flex items-end gap-2 pb-1">
                <Controller
                  name="isPublic"
                  control={control}
                  render={({ field }) => (
                    <Switch id="isPublic" checked={field.value} onCheckedChange={field.onChange} />
                  )}
                />
                <Label htmlFor="isPublic" className="text-[12.5px] font-medium">Public</Label>
              </div>
            </div>

            <div className="flex items-center justify-end gap-2 border-t border-border pt-3">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => { navigate({ to: '/admin/products' }).catch(() => undefined); }}
              >
                Cancel
              </Button>
              <Button type="submit" size="sm" disabled={isSubmitting || editMutation.isPending}>
                Save changes
              </Button>
            </div>
          </form>
        </DetailCard>

        <DetailCard title="Image">
          <div className="space-y-3">
            {product.imageUrl ? (
              <img
                src={`/api${product.imageUrl}?v=${imageVersion}`}
                alt=""
                className="aspect-video w-full rounded-md border border-border object-cover"
              />
            ) : (
              <div
                className="flex aspect-video w-full items-center justify-center rounded-md border border-border bg-surface-sunken text-ink-subtle"
                style={{
                  backgroundImage:
                    'repeating-linear-gradient(135deg, color-mix(in oklab, var(--foreground) 5%, transparent) 0 1px, transparent 1px 9px)',
                }}
              >
                <ImageOff className="size-6" aria-hidden="true" />
              </div>
            )}
            <div className="flex flex-wrap items-center gap-2">
              <ImagePickerButton
                label={product.imageUrl ? 'Replace image' : 'Upload image'}
                onSelect={(f) => { imageMutation.mutate(f); }}
                disabled={imageMutation.isPending}
              />
              {product.imageUrl && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() => { imageDeleteMutation.mutate(); }}
                  disabled={imageDeleteMutation.isPending}
                >
                  Remove image
                </Button>
              )}
            </div>
            <p className="text-[11px] text-ink-subtle">
              {product.pageContent
                ? 'This product has a rich product page.'
                : 'No rich product page yet.'}
            </p>
          </div>
        </DetailCard>
      </div>

      <ProductDownloadsCard
        productId={id}
        files={filesQuery.data}
        isPending={filesQuery.isPending}
        isError={filesQuery.isError}
      />
      <ProductLicencesCard productId={id} />
    </div>
  );
}

function StatCell({ label, value }: Readonly<{ label: string; value: string }>) {
  return (
    <div className="bg-card px-3 py-3">
      <div className="text-[10.5px] font-medium uppercase tracking-wide text-ink-muted">{label}</div>
      <div className="mt-1 text-[16px] font-semibold tabular-nums text-foreground">{value}</div>
    </div>
  );
}

function DetailCard({ title, children, action }: Readonly<{ title: string; children: React.ReactNode; action?: React.ReactNode }>) {
  return (
    <div className="overflow-hidden rounded-md border border-border bg-card shadow-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-2.5">
        <h2 className="text-[13px] font-semibold text-foreground">{title}</h2>
        {action}
      </div>
      <div className="p-4">{children}</div>
    </div>
  );
}

function Field({
  label,
  htmlFor,
  error,
  children,
}: Readonly<{
  label: string;
  htmlFor: string;
  error?: string;
  children: React.ReactNode;
}>) {
  return (
    <div className="space-y-1">
      <Label htmlFor={htmlFor} className="text-[11px] font-medium uppercase tracking-wide text-ink-muted">
        {label}
      </Label>
      {children}
      {error && <p className="text-[11px] text-status-revoked-fg">{error}</p>}
    </div>
  );
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

interface ProductDownloadsCardProps {
  productId: string;
  files: ProductFileResponse[] | undefined;
  isPending: boolean;
  isError: boolean;
}

function ProductDownloadsCard({ productId, files, isPending, isError }: Readonly<ProductDownloadsCardProps>) {
  const queryClient = useQueryClient();
  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadProductFile(productId, file),
    onSuccess: async (uploaded) => {
      await queryClient.invalidateQueries({ queryKey: ['products', 'files', productId] });
      toast.success(`Uploaded version ${uploaded.versionNumber}.`);
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the file.'));
    },
  });

  const inputId = useId();
  const items = files ?? [];
  const isEmpty = files !== undefined && items.length === 0;

  const downloadRevision = async (file: ProductFileResponse) => {
    try {
      const blob = await downloadProductFileRevision(productId, file.id);
      triggerBlobDownload(blob, file.fileName);
    } catch (error) {
      toast.error(errorDetail(error, 'Could not download this revision.'));
    }
  };

  const uploadLabel = items.length > 0 ? 'Upload new revision' : 'Upload first revision';

  let revisionsBody: React.ReactNode;
  if (isPending) {
    revisionsBody = <Skeleton className="h-16 w-full" />;
  } else if (isError) {
    revisionsBody = <p className="text-[12.5px] text-status-revoked-fg">Failed to load revisions.</p>;
  } else if (isEmpty) {
    revisionsBody = <p className="text-[12.5px] text-ink-muted">No revisions uploaded yet.</p>;
  } else {
    revisionsBody = (
      <Table className="text-[12.5px]">
        <TableHeader>
          <TableRow className="border-border">
            <Th className="w-[80px]">Version</Th>
            <Th>File</Th>
            <Th className="w-[100px] text-right">Size</Th>
            <Th className="w-[140px]">Uploaded</Th>
            <Th className="w-[60px]" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {items.map((f, idx) => (
            <TableRow key={f.id} className="border-border">
              <Td>
                <span className="font-mono text-[11.5px] text-foreground">v{f.versionNumber}</span>
                {idx === 0 && (
                  <span className="ml-2 rounded-[3px] border border-status-active-fg/30 bg-status-active-bg px-1.5 py-0 font-mono text-[10.5px] leading-[1.5] text-status-active-fg">
                    latest
                  </span>
                )}
              </Td>
              <Td>
                <span className="font-mono text-[11.5px] text-foreground">{f.fileName}</span>
              </Td>
              <Td className="text-right font-mono text-[11.5px] text-ink-muted">{formatFileSize(f.fileSizeBytes)}</Td>
              <Td className="font-mono text-[11px] text-ink-muted">{formatRelative(f.uploadedAt)}</Td>
              <Td>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="size-7"
                  onClick={() => { void downloadRevision(f); }}
                  aria-label={`Download v${f.versionNumber}`}
                >
                  <Download className="size-3.5" aria-hidden />
                </Button>
              </Td>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    );
  }

  return (
    <DetailCard
      title="Revisions"
      action={
        <>
          <label
            htmlFor={inputId}
            className={cn(
              buttonVariants({ variant: 'outline', size: 'sm' }),
              'cursor-pointer gap-1.5',
              uploadMutation.isPending && 'pointer-events-none opacity-50',
            )}
          >
            <Upload className="size-3.5" aria-hidden />
            {uploadLabel}
          </label>
          <input
            id={inputId}
            type="file"
            className="sr-only"
            aria-label={uploadLabel}
            disabled={uploadMutation.isPending}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) uploadMutation.mutate(f);
              e.target.value = '';
            }}
          />
        </>
      }
    >
      {revisionsBody}
    </DetailCard>
  );
}

const LICENCES_PAGE_SIZE = 25;

function ProductLicencesCard({ productId }: Readonly<{ productId: string }>) {
  const [offset, setOffset] = useState(0);
  const query = useQuery({
    queryKey: ['licences', 'list', { productId, offset }],
    queryFn: () => fetchLicences({ productId, limit: LICENCES_PAGE_SIZE, offset }),
    placeholderData: keepPreviousData,
  });

  const data = query.data;
  const totalPages = data ? Math.max(1, Math.ceil(data.total / LICENCES_PAGE_SIZE)) : 1;
  const currentPage = Math.floor(offset / LICENCES_PAGE_SIZE) + 1;
  const rangeStart = data && data.total > 0 ? data.offset + 1 : 0;
  const rangeEnd = data ? Math.min(data.offset + data.limit, data.total) : 0;

  return (
    <DetailCard
      title="Licences"
      action={
        <Link
          to="/admin/licences"
          search={{}}
          className="inline-flex items-center gap-1 text-[11.5px] font-medium text-accent hover:underline"
        >
          <Plus className="size-3" /> New
        </Link>
      }
    >
      <div className="-mx-4 -mt-4 overflow-hidden">
        <Table className="text-[12.5px]">
          <TableHeader>
            <TableRow className="border-border">
              <Th>Customer</Th>
              <Th className="w-[100px]">Status</Th>
              <Th className="w-[90px]">HWID</Th>
              <Th className="w-[110px]">Expires</Th>
              <Th className="w-[110px]">Created</Th>
              <Th className="w-[60px]" />
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
                  Failed to load licences.
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((lic) => (
              <TableRow key={lic.id} className="border-border hover:bg-surface-sunken">
                <Td>{lic.userEmail}</Td>
                <Td>
                  <StatusPill status={lic.status} />
                </Td>
                <Td>
                  {lic.hwidBound ? (
                    <span className="inline-flex items-center gap-1 text-[12px] text-status-active-fg">
                      <span aria-hidden className="size-1.5 rounded-full bg-status-active-fg" /> Bound
                    </span>
                  ) : (
                    <span className="text-ink-subtle">-</span>
                  )}
                </Td>
                <Td className="font-mono text-[11.5px] text-ink-muted">
                  {lic.expiresAt ? formatDate(lic.expiresAt) : '-'}
                </Td>
                <Td className="font-mono text-[11.5px] text-ink-muted">{formatDate(lic.createdAt)}</Td>
                <Td>
                  <Link
                    to="/admin/licences/$id"
                    params={{ id: lic.id }}
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
                  No licences for this product yet.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
      <div className="flex items-center justify-between border-t border-border px-1 pt-3 text-[12px] text-ink-muted">
        <span className="font-mono tabular-nums">
          {rangeStart}-{rangeEnd} of {data?.total ?? 0}
        </span>
        <div className="flex items-center gap-1.5">
          <Button
            variant="outline"
            size="icon"
            className="size-7"
            disabled={offset === 0}
            onClick={() => setOffset(Math.max(0, offset - LICENCES_PAGE_SIZE))}
            aria-label="Previous page"
          >
            <ChevronLeft className="size-3.5" />
          </Button>
          <span className="font-mono text-[11.5px] tabular-nums">
            {currentPage} / {totalPages}
          </span>
          <Button
            variant="outline"
            size="icon"
            className="size-7"
            disabled={!data || offset + LICENCES_PAGE_SIZE >= data.total}
            onClick={() => setOffset(offset + LICENCES_PAGE_SIZE)}
            aria-label="Next page"
          >
            <ChevronRight className="size-3.5" />
          </Button>
        </div>
      </div>
    </DetailCard>
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
