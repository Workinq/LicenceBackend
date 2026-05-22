import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  getMeOrders: vi.fn(),
  getMeOrdersId: vi.fn(),
  getAdminOrders: vi.fn(),
  getAdminOrdersId: vi.fn(),
}));

import {
  getMeOrders,
  getMeOrdersId,
  getAdminOrders,
  getAdminOrdersId,
} from '../api/generated/api';
import {
  fetchAdminOrder,
  fetchAdminOrders,
  fetchMyOrder,
  fetchMyOrders,
} from '../api/orders';

beforeEach(() => {
  vi.mocked(getMeOrders).mockReset();
  vi.mocked(getMeOrdersId).mockReset();
  vi.mocked(getAdminOrders).mockReset();
  vi.mocked(getAdminOrdersId).mockReset();
});

describe('api/orders', () => {
  it('fetchMyOrders forwards params and unwraps .data', async () => {
    const page = { items: [], total: 0, limit: 5, offset: 0 };
    vi.mocked(getMeOrders).mockResolvedValue({ data: page } as never);
    expect(await fetchMyOrders({ limit: 5 } as never)).toEqual(page);
    expect(getMeOrders).toHaveBeenCalledWith({ limit: 5 });
  });

  it('fetchMyOrders defaults params to an empty object', async () => {
    vi.mocked(getMeOrders).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 } } as never);
    await fetchMyOrders();
    expect(getMeOrders).toHaveBeenCalledWith({});
  });

  it('fetchMyOrder calls getMeOrdersId with the id', async () => {
    vi.mocked(getMeOrdersId).mockResolvedValue({ data: { id: 'o1' } } as never);
    expect(await fetchMyOrder('o1')).toEqual({ id: 'o1' });
    expect(getMeOrdersId).toHaveBeenCalledWith('o1');
  });

  it('fetchAdminOrders forwards params and unwraps .data', async () => {
    const page = { items: [{ id: 'o1' }], total: 1, limit: 10, offset: 0 };
    vi.mocked(getAdminOrders).mockResolvedValue({ data: page } as never);
    expect(await fetchAdminOrders({ limit: 10 } as never)).toEqual(page);
    expect(getAdminOrders).toHaveBeenCalledWith({ limit: 10 });
  });

  it('fetchAdminOrders defaults params to an empty object', async () => {
    vi.mocked(getAdminOrders).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 } } as never);
    await fetchAdminOrders();
    expect(getAdminOrders).toHaveBeenCalledWith({});
  });

  it('fetchAdminOrder calls getAdminOrdersId with the id', async () => {
    vi.mocked(getAdminOrdersId).mockResolvedValue({ data: { id: 'o2' } } as never);
    expect(await fetchAdminOrder('o2')).toEqual({ id: 'o2' });
    expect(getAdminOrdersId).toHaveBeenCalledWith('o2');
  });
});
