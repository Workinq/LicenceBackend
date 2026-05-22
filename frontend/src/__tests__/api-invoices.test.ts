import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  getMeOrdersIdInvoice: vi.fn(),
  getAdminOrdersIdInvoice: vi.fn(),
}));

import { getMeOrdersIdInvoice, getAdminOrdersIdInvoice } from '../api/generated/api';
import { fetchAdminInvoice, fetchMyInvoice } from '../api/invoices';

beforeEach(() => {
  vi.mocked(getMeOrdersIdInvoice).mockReset();
  vi.mocked(getAdminOrdersIdInvoice).mockReset();
});

describe('api/invoices', () => {
  it('fetchMyInvoice calls getMeOrdersIdInvoice with the order id', async () => {
    const invoice = { id: 'inv1', orderId: 'o1' };
    vi.mocked(getMeOrdersIdInvoice).mockResolvedValue({ data: invoice } as never);
    expect(await fetchMyInvoice('o1')).toEqual(invoice);
    expect(getMeOrdersIdInvoice).toHaveBeenCalledWith('o1');
  });

  it('fetchAdminInvoice calls getAdminOrdersIdInvoice with the order id', async () => {
    const invoice = { id: 'inv2', orderId: 'o2' };
    vi.mocked(getAdminOrdersIdInvoice).mockResolvedValue({ data: invoice } as never);
    expect(await fetchAdminInvoice('o2')).toEqual(invoice);
    expect(getAdminOrdersIdInvoice).toHaveBeenCalledWith('o2');
  });
});
