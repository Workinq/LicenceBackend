import { useState } from 'react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { createProduct } from '@/api/products';
import { ApiError } from '@/auth/api-client';

export const Route = createFileRoute('/_authed/products_/new')({
  component: NewProductPage,
});

const schema = z.object({
  slug: z
    .string()
    .min(1, 'Required')
    .regex(/^[a-z0-9-]+$/, 'Lowercase letters, numbers, and hyphens only'),
  displayName: z.string().min(1, 'Display name is required'),
});

type FormValues = z.infer<typeof schema>;

function NewProductPage() {
  const navigate = useNavigate();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { slug: '', displayName: '' } });

  const mutation = useMutation({
    mutationFn: (values: FormValues) => createProduct({ slug: values.slug, displayName: values.displayName, description: null, tagline: null, isPublic: null, price: null, currency: null, sortOrder: null }),
    onSuccess: () => {
      toast.success('Product created.');
      void navigate({ to: '/products' });
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

        <Button type="submit" disabled={isSubmitting || mutation.isPending}>
          Create product
        </Button>
      </form>
    </div>
  );
}
