import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { createProduct } from '@/api/products';
import { ApiError } from '@/auth/api-client';
import type { ProductResponse } from '@/api/generated/api.schemas';

const schema = z.object({
  slug: z
    .string()
    .min(1, 'Required')
    .regex(/^[a-z0-9-]+$/, 'Lowercase letters, numbers, and hyphens only'),
  displayName: z.string().min(1, 'Display name is required'),
});

type FormValues = z.infer<typeof schema>;

interface QuickCreateProductDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (product: ProductResponse) => void;
}

export function QuickCreateProductDialog({ open, onOpenChange, onCreated }: Readonly<QuickCreateProductDialogProps>) {
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { slug: '', displayName: '' } });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createProduct({
        slug: values.slug,
        displayName: values.displayName,
        description: null,
        tagline: null,
        isPublic: null,
        price: null,
        currency: null,
        sortOrder: null,
      }),
    onSuccess: (product) => {
      onCreated(product);
      reset();
      setSubmitError(null);
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
    try {
      await mutation.mutateAsync(values);
    } catch {
      // Surface handled via onError; swallow here so the form does not throw.
    }
  };

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      reset();
      setSubmitError(null);
    }
    onOpenChange(next);
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New product</DialogTitle>
          <DialogDescription>
            Just the essentials. You can edit the rest from the product page later.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
          {submitError && (
            <Alert variant="destructive">
              <AlertDescription>{submitError}</AlertDescription>
            </Alert>
          )}

          <div className="space-y-1">
            <Label htmlFor="qc-product-slug">Slug</Label>
            <Input id="qc-product-slug" placeholder="acme-pro" {...register('slug')} />
            {errors.slug && <p className="text-xs text-status-revoked-fg">{errors.slug.message}</p>}
          </div>

          <div className="space-y-1">
            <Label htmlFor="qc-product-name">Display name</Label>
            <Input id="qc-product-name" placeholder="Acme Pro" {...register('displayName')} />
            {errors.displayName && (
              <p className="text-xs text-status-revoked-fg">{errors.displayName.message}</p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => { handleOpenChange(false); }}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting || mutation.isPending}>
              Create product
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
