import { useState } from 'react';
import { createFileRoute, useNavigate, Link } from '@tanstack/react-router';
import { useQuery, useMutation } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import { fetchProducts } from '@/api/products';
import { fetchUsers } from '@/api/users';
import { createLicence } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceCreatedResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/_authed/licences/new')({
  component: NewLicencePage,
});

const schema = z.object({
  productId: z.string().min(1, 'Pick a product'),
  userId: z.string().min(1, 'Pick a user'),
  expiresAt: z.string().optional(),
  notes: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function NewLicencePage() {
  const navigate = useNavigate();
  const [created, setCreated] = useState<LicenceCreatedResponse | null>(null);

  const products = useQuery({ queryKey: ['products'], queryFn: fetchProducts });
  const users = useQuery({ queryKey: ['users'], queryFn: fetchUsers });

  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createLicence({
        productId: values.productId,
        userId: values.userId,
        email: null,
        expiresAt: values.expiresAt ? new Date(values.expiresAt).toISOString() : null,
        notes: values.notes ? values.notes : null,
      }),
    onSuccess: (data) => {
      setCreated(data);
    },
    onError: (error) => {
      toast.error(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not create the licence.',
      );
    },
  });

  const onSubmit = async (values: FormValues) => {
    await mutation.mutateAsync(values);
  };

  if (created) {
    return (
      <div className="max-w-2xl space-y-6">
        <h1 className="font-display text-2xl font-semibold text-ink">Licence created</h1>
        <SecretRevealOnce label="Licence key" value={created.licenceKey} />
        <Button onClick={() => { void navigate({ to: '/licences/$id', params: { id: created.id } }); }}>
          Go to licence
        </Button>
      </div>
    );
  }

  if (products.isPending || users.isPending) {
    return <Skeleton className="h-64 w-full max-w-2xl" />;
  }

  if (!products.data || products.data.items.length === 0) {
    return (
      <div className="max-w-2xl space-y-3">
        <p className="text-sm text-ink-muted">No products available. Create a product first.</p>
        <Link to="/products">Go to products</Link>
      </div>
    );
  }

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">New licence</h1>

      <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
        <div className="space-y-1">
          <Label htmlFor="productId">Product</Label>
          <Controller
            name="productId"
            control={control}
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="productId" className="w-full">
                  <SelectValue placeholder="Choose one..." />
                </SelectTrigger>
                <SelectContent>
                  {products.data.items.map((p) => (
                    <SelectItem key={p.id} value={p.id}>{p.displayName}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.productId && (
            <p className="text-xs text-status-revoked-fg">{errors.productId.message}</p>
          )}
        </div>

        <div className="space-y-1">
          <Label htmlFor="userId">User</Label>
          <Controller
            name="userId"
            control={control}
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger id="userId" className="w-full">
                  <SelectValue placeholder="Choose one..." />
                </SelectTrigger>
                <SelectContent>
                  {users.data?.items.map((u) => (
                    <SelectItem key={u.id} value={u.id}>{u.email}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.userId && (
            <p className="text-xs text-status-revoked-fg">{errors.userId.message}</p>
          )}
        </div>

        <div className="space-y-1">
          <Label htmlFor="expiresAt">Expires (optional)</Label>
          <Input id="expiresAt" type="date" {...register('expiresAt')} />
        </div>

        <div className="space-y-1">
          <Label htmlFor="notes">Notes (optional)</Label>
          <Textarea id="notes" {...register('notes')} />
        </div>

        <Button type="submit" disabled={isSubmitting || mutation.isPending}>
          Create licence
        </Button>
      </form>
    </div>
  );
}
