import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { CurrencyCombobox } from '@/components/CurrencyCombobox';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ImagePickerButton } from '@/components/ImagePickerButton';
import { createProduct, uploadProductImage } from '@/api/products';
import type { ProductResponse } from '@/api/generated/api.schemas';
import { ApiError } from '@/auth/api-client';

export const Route = createFileRoute('/admin/products_/new')({
  component: NewProductPage,
});

const schema = z.object({
  slug: z
    .string()
    .min(1, 'Required')
    .regex(/^[a-z0-9-]+$/, 'Lowercase letters, numbers, and hyphens only'),
  displayName: z.string().min(1, 'Display name is required'),
  description: z.string().optional(),
  tagline: z.string().optional(),
  isPublic: z.boolean(),
  price: z.string().optional(),
  currency: z.string().regex(/^[A-Z]{3}$/, 'Three uppercase letters').optional().or(z.literal('')),
  sortOrder: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function NewProductPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [stagedImage, setStagedImage] = useState<File | null>(null);

  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { slug: '', displayName: '', description: '', tagline: '', isPublic: true, price: '', currency: 'USD', sortOrder: '0' },
  });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createProduct({
        slug: values.slug,
        displayName: values.displayName,
        description: values.description ? values.description : null,
        tagline: values.tagline ? values.tagline : null,
        isPublic: values.isPublic,
        price: values.price ? Number(values.price) : null,
        currency: values.currency ? values.currency : null,
        sortOrder: values.sortOrder ? Number(values.sortOrder) : null,
      }),
    onSuccess: async (created: ProductResponse) => {
      if (stagedImage) {
        try {
          await uploadProductImage(created.id, stagedImage);
        } catch {
          await queryClient.invalidateQueries({ queryKey: ['products'] });
          toast.error('Product created, but the image upload failed. You can add it from the product page.');
          await navigate({ to: '/admin/products/$id', params: { id: created.id } });
          return;
        }
      }
      await queryClient.invalidateQueries({ queryKey: ['products'] });
      toast.success('Product created.');
      await navigate({ to: '/admin/products' });
    },
    onError: (error) => {
      setSubmitError(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not create the product.',
      );
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSubmitError(null);
    await mutation.mutateAsync(values);
  };

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">New product</h1>

      <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
        {submitError && (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-1">
          <Label htmlFor="slug">Slug</Label>
          <Input id="slug" placeholder="acme-pro" {...register('slug')} />
          {errors.slug && <p className="text-xs text-status-revoked-fg">{errors.slug.message}</p>}
        </div>

        <div className="space-y-1">
          <Label htmlFor="displayName">Display name</Label>
          <Input id="displayName" placeholder="Acme Pro" {...register('displayName')} />
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

        <div className="space-y-1">
          <p className="text-sm font-medium text-ink">Image (optional)</p>
          <div className="flex items-center gap-3">
            <ImagePickerButton label="Choose image" onSelect={setStagedImage} disabled={mutation.isPending} />
            {stagedImage && (
              <span className="text-sm text-ink-muted">
                {stagedImage.name}{' '}
                <button
                  type="button"
                  className="text-status-revoked-fg underline underline-offset-2"
                  onClick={() => { setStagedImage(null); }}
                >
                  remove
                </button>
              </span>
            )}
          </div>
        </div>

        <div className="flex gap-3">
          <Button type="submit" disabled={isSubmitting || mutation.isPending}>
            Create product
          </Button>
          <Button type="button" variant="outline" onClick={() => { navigate({ to: '/admin/products' }).catch(() => undefined); }}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
