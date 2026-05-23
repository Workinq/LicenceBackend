import { useQuery } from '@tanstack/react-query';
import { fetchLicences } from '@/api/licences';
import { fetchMyLicences } from '@/api/me-licences';
import { fetchProducts } from '@/api/products';
import { fetchAdminOrders, fetchMyOrders } from '@/api/orders';
import { fetchUsers } from '@/api/users';

const COUNT_OPTS = {
  staleTime: 30_000,
  refetchOnWindowFocus: false,
} as const;

export function useAdminSidebarCounts() {
  const licences = useQuery({
    queryKey: ['admin-sidebar-count', 'licences'],
    queryFn: () => fetchLicences({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });
  const products = useQuery({
    queryKey: ['admin-sidebar-count', 'products'],
    queryFn: () => fetchProducts({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });
  const orders = useQuery({
    queryKey: ['admin-sidebar-count', 'orders'],
    queryFn: () => fetchAdminOrders({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });
  const users = useQuery({
    queryKey: ['admin-sidebar-count', 'users'],
    queryFn: () => fetchUsers({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });

  return {
    licences: licences.data?.total,
    products: products.data?.total,
    orders: orders.data?.total,
    users: users.data?.total,
  };
}

export function usePortalSidebarCounts() {
  const licences = useQuery({
    queryKey: ['portal-sidebar-count', 'licences'],
    queryFn: () => fetchMyLicences({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });
  const products = useQuery({
    queryKey: ['portal-sidebar-count', 'products'],
    queryFn: () => fetchProducts({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });
  const orders = useQuery({
    queryKey: ['portal-sidebar-count', 'orders'],
    queryFn: () => fetchMyOrders({ limit: 1, offset: 0 }),
    ...COUNT_OPTS,
  });

  return {
    licences: licences.data?.total,
    products: products.data?.total,
    orders: orders.data?.total,
  };
}
