import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  deleteLicencesCheckoutsSeatId: vi.fn(),
}));

import { deleteLicencesCheckoutsSeatId } from '../api/generated/api';
import { checkinSeat } from '../api/checkouts';

beforeEach(() => {
  vi.mocked(deleteLicencesCheckoutsSeatId).mockReset();
});

describe('api/checkouts', () => {
  it('checkinSeat calls deleteLicencesCheckoutsSeatId with the seat id', async () => {
    vi.mocked(deleteLicencesCheckoutsSeatId).mockResolvedValue({ data: undefined } as never);
    await expect(checkinSeat('seat-1')).resolves.toBeUndefined();
    expect(deleteLicencesCheckoutsSeatId).toHaveBeenCalledWith('seat-1');
  });
});
