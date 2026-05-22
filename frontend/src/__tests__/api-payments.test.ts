import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  postPaymentsCheckout: vi.fn(),
  getPaymentsCheckoutId: vi.fn(),
  getPaymentsConfig: vi.fn(),
}));

import {
  postPaymentsCheckout,
  getPaymentsCheckoutId,
  getPaymentsConfig,
} from '../api/generated/api';
import {
  fetchCheckoutStatus,
  fetchPaymentConfig,
  startCheckout,
} from '../api/payments';

beforeEach(() => {
  vi.mocked(postPaymentsCheckout).mockReset();
  vi.mocked(getPaymentsCheckoutId).mockReset();
  vi.mocked(getPaymentsConfig).mockReset();
});

describe('api/payments', () => {
  it('startCheckout forwards the body and unwraps .data', async () => {
    const body = { productId: 'p1', quantity: 1, currency: 'usd' };
    const session = { id: 'cs1', clientSecret: 'pi_abc' };
    vi.mocked(postPaymentsCheckout).mockResolvedValue({ data: session } as never);
    expect(await startCheckout(body as never)).toEqual(session);
    expect(postPaymentsCheckout).toHaveBeenCalledWith(body);
  });

  it('fetchCheckoutStatus calls getPaymentsCheckoutId with the id', async () => {
    vi.mocked(getPaymentsCheckoutId).mockResolvedValue({ data: { status: 'complete' } } as never);
    expect(await fetchCheckoutStatus('cs1')).toEqual({ status: 'complete' });
    expect(getPaymentsCheckoutId).toHaveBeenCalledWith('cs1');
  });

  it('fetchPaymentConfig calls getPaymentsConfig and unwraps .data', async () => {
    const cfg = { publishableKey: 'pk_test_123' };
    vi.mocked(getPaymentsConfig).mockResolvedValue({ data: cfg } as never);
    expect(await fetchPaymentConfig()).toEqual(cfg);
    expect(getPaymentsConfig).toHaveBeenCalledTimes(1);
  });
});
