import { useState } from 'react';
import { Plus } from 'lucide-react';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm, Controller } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Combobox } from '@/components/Combobox';
import { CidrListEditor } from '@/components/licences/CidrListEditor';
import { SecretRevealOnce } from '@/components/SecretRevealOnce';
import { QuickCreateProductDialog } from '@/components/QuickCreateProductDialog';
import { QuickCreateUserDialog } from '@/components/QuickCreateUserDialog';
import { fetchProducts } from '@/api/products';
import { fetchUsers } from '@/api/users';
import { createLicence } from '@/api/licences';
import { ApiError } from '@/auth/api-client';
import type { LicenceCreatedResponse } from '@/api/generated/api.schemas';

export const Route = createFileRoute('/admin/licences_/new')({
  component: NewLicencePage,
});

const schema = z.object({
  productId: z.string().min(1, 'Choose a product'),
  userId: z.string().min(1, 'Choose a user'),
  expiresAt: z.string().optional(),
  notes: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

function NewLicencePage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [created, setCreated] = useState<LicenceCreatedResponse | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [ipRestricted, setIpRestricted] = useState(false);
  const [ipCidrs, setIpCidrs] = useState<string[]>([]);
  const [productDialogOpen, setProductDialogOpen] = useState(false);
  const [userDialogOpen, setUserDialogOpen] = useState(false);

  const products = useQuery({ queryKey: ['products', 'picker'], queryFn: () => fetchProducts({ limit: 200 }) });
  const users = useQuery({ queryKey: ['users', 'picker'], queryFn: () => fetchUsers({ limit: 200, offset: 0 }) });

  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { productId: '', userId: '', expiresAt: '', notes: '' } });

  const mutation = useMutation({
    mutationFn: (values: FormValues) =>
      createLicence({
        productId: values.productId,
        userId: values.userId,
        email: null,
        expiresAt: values.expiresAt ? new Date(values.expiresAt).toISOString() : null,
        notes: values.notes ? values.notes : null,
        ipAllowlist: ipRestricted ? ipCidrs.map((c) => c.trim()).filter((c) => c.length > 0) : null,
      }),
    onSuccess: (data) => {
      void queryClient.invalidateQueries({ queryKey: ['licences', 'list'] });
      setCreated(data);
    },
    onError: (error) => {
      setSubmitError(
        error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
          ? String((error.body as Record<string, unknown>).detail)
          : 'Could not create the licence.',
      );
    },
  });

  const onSubmit = async (values: FormValues) => {
    setSubmitError(null);
    await mutation.mutateAsync(values);
  };

  if (created) {
    return (
      <div className="max-w-2xl space-y-6">
        <h1 className="font-display text-2xl font-semibold text-ink">Licence created</h1>
        <SecretRevealOnce label="Licence key" value={created.licenceKey} />
        <Button onClick={() => { void navigate({ to: '/admin/licences/$id', params: { id: created.id } }); }}>
          Go to licence
        </Button>
      </div>
    );
  }

  if (products.isPending || users.isPending) {
    return <Skeleton className="h-64 w-full max-w-2xl" />;
  }

  const productOptions = (products.data?.items ?? []).map((p) => ({ value: p.id, label: p.displayName }));
  const userOptions = (users.data?.items ?? []).map((u) => ({ value: u.id, label: u.email }));

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="font-display text-2xl font-semibold text-ink">New licence</h1>

      <form onSubmit={(e) => { void handleSubmit(onSubmit)(e); }} noValidate className="space-y-4">
        {submitError && (
          <Alert variant="destructive">
            <AlertDescription>{submitError}</AlertDescription>
          </Alert>
        )}

        <div className="space-y-1">
          <Label htmlFor="productId">Product</Label>
          <Controller
            name="productId"
            control={control}
            render={({ field }) => (
              <Combobox
                id="productId"
                options={productOptions}
                value={field.value ?? ''}
                onChange={field.onChange}
                placeholder="Choose..."
                searchPlaceholder="Search products"
                emptyText={productOptions.length === 0 ? 'There are no products' : 'No matching products'}
                footerAction={{
                  label: 'Create new product',
                  icon: <Plus className="size-4" aria-hidden="true" />,
                  onSelect: () => { setProductDialogOpen(true); },
                }}
              />
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
              <Combobox
                id="userId"
                options={userOptions}
                value={field.value ?? ''}
                onChange={field.onChange}
                placeholder="Choose..."
                searchPlaceholder="Search users"
                emptyText={userOptions.length === 0 ? 'There are no users' : 'No matching users'}
                footerAction={{
                  label: 'Create new user',
                  icon: <Plus className="size-4" aria-hidden="true" />,
                  onSelect: () => { setUserDialogOpen(true); },
                }}
              />
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

        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Switch
              id="ip-restrict"
              checked={ipRestricted}
              onCheckedChange={setIpRestricted}
              aria-label="Restrict by IP address"
            />
            <Label htmlFor="ip-restrict">Restrict by IP address</Label>
          </div>
          {ipRestricted && (
            <div className="space-y-2">
              <p className="text-xs text-ink-muted">
                Leave empty and the first IP that verifies this licence will be locked in automatically.
              </p>
              <CidrListEditor cidrs={ipCidrs} onChange={setIpCidrs} />
            </div>
          )}
        </div>

        <Button type="submit" disabled={isSubmitting || mutation.isPending}>
          Create licence
        </Button>
      </form>

      <QuickCreateProductDialog
        open={productDialogOpen}
        onOpenChange={setProductDialogOpen}
        onCreated={(product) => {
          void queryClient.invalidateQueries({ queryKey: ['products'] });
          setValue('productId', product.id, { shouldValidate: true });
          setProductDialogOpen(false);
        }}
      />

      <QuickCreateUserDialog
        open={userDialogOpen}
        onOpenChange={setUserDialogOpen}
        onCreated={(user) => {
          void queryClient.invalidateQueries({ queryKey: ['users'] });
          setValue('userId', user.id, { shouldValidate: true });
        }}
      />
    </div>
  );
}
