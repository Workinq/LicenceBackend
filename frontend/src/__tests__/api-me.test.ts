import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  getMe: vi.fn(),
  patchMe: vi.fn(),
  patchMePassword: vi.fn(),
}));

import { getMe, patchMe, patchMePassword } from '../api/generated/api';
import { fetchMe, updateProfile, changePassword } from '../api/me';

beforeEach(() => {
  vi.mocked(getMe).mockReset();
  vi.mocked(patchMe).mockReset();
  vi.mocked(patchMePassword).mockReset();
});

describe('api/me', () => {
  it('fetchMe calls getMe and unwraps .data', async () => {
    vi.mocked(getMe).mockResolvedValue({ data: { id: 'u1' } } as never);
    expect(await fetchMe()).toEqual({ id: 'u1' });
    expect(getMe).toHaveBeenCalledTimes(1);
  });

  it('updateProfile forwards the body to patchMe and unwraps .data', async () => {
    const body = { displayName: 'Alice' };
    vi.mocked(patchMe).mockResolvedValue({ data: { id: 'u1', displayName: 'Alice' } } as never);
    expect(await updateProfile(body as never)).toEqual({ id: 'u1', displayName: 'Alice' });
    expect(patchMe).toHaveBeenCalledWith(body);
  });

  it('changePassword forwards the body to patchMePassword and resolves void', async () => {
    const body = { currentPassword: 'old', newPassword: 'new' };
    vi.mocked(patchMePassword).mockResolvedValue({ data: undefined } as never);
    await expect(changePassword(body as never)).resolves.toBeUndefined();
    expect(patchMePassword).toHaveBeenCalledWith(body);
  });
});
