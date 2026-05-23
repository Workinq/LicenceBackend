import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command';
import { fetchLicences } from '@/api/licences';
import { fetchProducts } from '@/api/products';
import { fetchUsers } from '@/api/users';
import { fetchMyLicences } from '@/api/me-licences';
import { useAccessTokenStore } from '@/auth/access-token-store';
import {
  KeyRound,
  LayoutDashboard,
  Package,
  Receipt,
  Users,
  FileText,
  ShoppingBasket,
  User,
} from 'lucide-react';

interface PageRoute {
  label: string;
  to: string;
  Icon: typeof KeyRound;
}

const ADMIN_PAGES: PageRoute[] = [
  { label: 'Overview', to: '/admin', Icon: LayoutDashboard },
  { label: 'Licences', to: '/admin/licences', Icon: KeyRound },
  { label: 'Products', to: '/admin/products', Icon: Package },
  { label: 'Orders', to: '/admin/orders', Icon: Receipt },
  { label: 'Users', to: '/admin/users', Icon: Users },
  { label: 'New licence', to: '/admin/licences/new', Icon: FileText },
  { label: 'New product', to: '/admin/products/new', Icon: FileText },
];

const PORTAL_PAGES: PageRoute[] = [
  { label: 'Overview', to: '/portal', Icon: LayoutDashboard },
  { label: 'My licences', to: '/portal/licences', Icon: KeyRound },
  { label: 'Catalogue', to: '/portal/products', Icon: Package },
  { label: 'My orders', to: '/portal/orders', Icon: Receipt },
  { label: 'Basket', to: '/portal/basket', Icon: ShoppingBasket },
  { label: 'Profile', to: '/portal/me', Icon: User },
];

export function CommandPalette() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const user = useAccessTokenStore((s) => s.user);
  const isAdmin = user?.role === 'admin';

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOpen((prev) => !prev);
      }
    };
    globalThis.addEventListener('keydown', onKey);
    return () => globalThis.removeEventListener('keydown', onKey);
  }, []);

  const pages = isAdmin ? ADMIN_PAGES : PORTAL_PAGES;

  // Use the same query cache patterns the rest of the app uses; staleTime keeps these snappy.
  const licencesQuery = useQuery({
    queryKey: isAdmin ? ['licences', 'list', { offset: 0 }] : ['my-licences-recent'],
    queryFn: () => (isAdmin ? fetchLicences({ limit: 6, offset: 0 }) : fetchMyLicences({ limit: 6, offset: 0 })),
    enabled: open,
    staleTime: 30_000,
  });

  const productsQuery = useQuery({
    queryKey: ['products-recent'],
    queryFn: () => fetchProducts({ limit: 6, offset: 0 }),
    enabled: open,
    staleTime: 30_000,
  });

  const usersQuery = useQuery({
    queryKey: ['users-recent'],
    queryFn: () => fetchUsers({ limit: 6, offset: 0 }),
    enabled: open && isAdmin,
    staleTime: 30_000,
  });

  const licences = useMemo(() => licencesQuery.data?.items ?? [], [licencesQuery.data]);
  const products = useMemo(() => productsQuery.data?.items ?? [], [productsQuery.data]);
  const users = useMemo(() => usersQuery.data?.items ?? [], [usersQuery.data]);

  const go = (path: string) => {
    setOpen(false);
    navigate({ to: path }).catch(() => undefined);
  };

  return (
    <CommandDialog
      open={open}
      onOpenChange={setOpen}
      title="Command palette"
      description="Jump to a page, licence, product, or person."
    >
      <CommandInput placeholder="Search licences, products, customers, pages..." />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>

        {licences.length > 0 && (
          <CommandGroup heading={isAdmin ? 'Recent licences' : 'My licences'}>
            {licences.map((lic) => (
              <CommandItem
                key={lic.id}
                value={`${lic.productSlug} ${lic.userEmail} ${lic.id}`}
                onSelect={() => go(isAdmin ? `/admin/licences/${lic.id}` : `/portal/licences/${lic.id}`)}
              >
                <KeyRound />
                <span className="font-mono text-[12px]">{lic.id.slice(0, 18)}</span>
                <span className="text-ink-muted">{lic.productSlug}</span>
                {isAdmin && <span className="ml-auto text-ink-subtle">{lic.userEmail}</span>}
              </CommandItem>
            ))}
          </CommandGroup>
        )}

        {products.length > 0 && (
          <CommandGroup heading="Products">
            {products.map((p) => (
              <CommandItem
                key={p.id}
                value={`${p.slug} ${p.displayName}`}
                onSelect={() => go(isAdmin ? `/admin/products/${p.id}` : `/portal/products/${p.id}`)}
              >
                <Package />
                <span className="font-mono text-[12px]">{p.slug}</span>
                <span className="text-ink-muted">{p.displayName}</span>
              </CommandItem>
            ))}
          </CommandGroup>
        )}

        {isAdmin && users.length > 0 && (
          <CommandGroup heading="Customers">
            {users.map((u) => (
              <CommandItem
                key={u.id}
                value={`${u.email} ${u.displayName ?? ''}`}
                onSelect={() => go(`/admin/users/${u.id}`)}
              >
                <User />
                <span>{u.email}</span>
                {u.displayName && <span className="ml-auto text-ink-subtle">{u.displayName}</span>}
              </CommandItem>
            ))}
          </CommandGroup>
        )}

        <CommandGroup heading="Pages">
          {pages.map((p) => {
            const Icon = p.Icon;
            return (
              <CommandItem key={p.to} value={p.label} onSelect={() => go(p.to)}>
                <Icon />
                <span>{p.label}</span>
              </CommandItem>
            );
          })}
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
}
