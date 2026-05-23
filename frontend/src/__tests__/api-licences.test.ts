import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  deleteLicencesIdMembersMemberId: vi.fn(),
  deleteLicencesIdSeatsSeatId: vi.fn(),
  getLicences: vi.fn(),
  getLicencesId: vi.fn(),
  getLicencesIdBindingHistory: vi.fn(),
  getLicencesIdMembers: vi.fn(),
  getLicencesIdSeats: vi.fn(),
  getLicencesIdStatusHistory: vi.fn(),
  getLicencesIdVerificationAttempts: vi.fn(),
  patchLicencesIdMaxSeats: vi.fn(),
  patchLicencesIdStatus: vi.fn(),
  postLicences: vi.fn(),
  postLicencesIdMembers: vi.fn(),
  postLicencesIdRegenerateKey: vi.fn(),
  putLicencesIdHwid: vi.fn(),
  putLicencesIdIpAllowlist: vi.fn(),
}));

import {
  deleteLicencesIdMembersMemberId,
  deleteLicencesIdSeatsSeatId,
  getLicences,
  getLicencesId,
  getLicencesIdBindingHistory,
  getLicencesIdMembers,
  getLicencesIdSeats,
  getLicencesIdStatusHistory,
  getLicencesIdVerificationAttempts,
  patchLicencesIdMaxSeats,
  patchLicencesIdStatus,
  postLicences,
  postLicencesIdMembers,
  postLicencesIdRegenerateKey,
  putLicencesIdHwid,
  putLicencesIdIpAllowlist,
} from '../api/generated/api';
import {
  addLicenceMember,
  createLicence,
  fetchLicence,
  fetchLicenceBindingHistory,
  fetchLicenceMembers,
  fetchLicenceSeats,
  fetchLicenceStatusHistory,
  fetchLicenceVerificationAttempts,
  fetchLicences,
  forceRevokeSeat,
  regenerateLicenceKey,
  removeLicenceMember,
  updateLicenceHwid,
  updateLicenceIpAllowlist,
  updateLicenceMaxSeats,
  updateLicenceStatus,
} from '../api/licences';

beforeEach(() => {
  vi.mocked(deleteLicencesIdMembersMemberId).mockReset();
  vi.mocked(deleteLicencesIdSeatsSeatId).mockReset();
  vi.mocked(getLicences).mockReset();
  vi.mocked(getLicencesId).mockReset();
  vi.mocked(getLicencesIdBindingHistory).mockReset();
  vi.mocked(getLicencesIdMembers).mockReset();
  vi.mocked(getLicencesIdSeats).mockReset();
  vi.mocked(getLicencesIdStatusHistory).mockReset();
  vi.mocked(getLicencesIdVerificationAttempts).mockReset();
  vi.mocked(patchLicencesIdMaxSeats).mockReset();
  vi.mocked(patchLicencesIdStatus).mockReset();
  vi.mocked(postLicences).mockReset();
  vi.mocked(postLicencesIdMembers).mockReset();
  vi.mocked(postLicencesIdRegenerateKey).mockReset();
  vi.mocked(putLicencesIdHwid).mockReset();
  vi.mocked(putLicencesIdIpAllowlist).mockReset();
});

describe('api/licences', () => {
  it('fetchLicences forwards params and unwraps .data', async () => {
    const page = { items: [{ id: 'lic-1' }], total: 1, limit: 10, offset: 0 };
    vi.mocked(getLicences).mockResolvedValue({ data: page } as never);
    expect(await fetchLicences({ limit: 10 } as never)).toEqual(page);
    expect(getLicences).toHaveBeenCalledWith({ limit: 10 });
  });

  it('fetchLicence calls getLicencesId with the id', async () => {
    vi.mocked(getLicencesId).mockResolvedValue({ data: { id: 'lic-1' } } as never);
    expect(await fetchLicence('lic-1')).toEqual({ id: 'lic-1' });
    expect(getLicencesId).toHaveBeenCalledWith('lic-1');
  });

  it('createLicence forwards the body and unwraps .data', async () => {
    const body = { productId: 'p', userId: 'u' };
    vi.mocked(postLicences).mockResolvedValue({ data: { id: 'lic-new', key: 'k' } } as never);
    expect(await createLicence(body as never)).toEqual({ id: 'lic-new', key: 'k' });
    expect(postLicences).toHaveBeenCalledWith(body);
  });

  it('updateLicenceStatus calls patchLicencesIdStatus with id and body', async () => {
    vi.mocked(patchLicencesIdStatus).mockResolvedValue({ data: { id: 'lic-1', status: 'revoked' } } as never);
    const body = { status: 'revoked', reason: null };
    expect(await updateLicenceStatus('lic-1', body as never)).toEqual({ id: 'lic-1', status: 'revoked' });
    expect(patchLicencesIdStatus).toHaveBeenCalledWith('lic-1', body);
  });

  it('regenerateLicenceKey calls postLicencesIdRegenerateKey with id and body', async () => {
    vi.mocked(postLicencesIdRegenerateKey).mockResolvedValue({ data: { key: 'new-key' } } as never);
    const body = { reason: null };
    expect(await regenerateLicenceKey('lic-1', body as never)).toEqual({ key: 'new-key' });
    expect(postLicencesIdRegenerateKey).toHaveBeenCalledWith('lic-1', body);
  });

  it('updateLicenceHwid calls putLicencesIdHwid and resolves void', async () => {
    vi.mocked(putLicencesIdHwid).mockResolvedValue({ data: undefined } as never);
    const body = { hwid: null, reason: null };
    await expect(updateLicenceHwid('lic-1', body as never)).resolves.toBeUndefined();
    expect(putLicencesIdHwid).toHaveBeenCalledWith('lic-1', body);
  });

  it('updateLicenceIpAllowlist calls putLicencesIdIpAllowlist and resolves void', async () => {
    vi.mocked(putLicencesIdIpAllowlist).mockResolvedValue({ data: undefined } as never);
    const body = { cidrs: ['10.0.0.0/8'], reason: null };
    await expect(updateLicenceIpAllowlist('lic-1', body as never)).resolves.toBeUndefined();
    expect(putLicencesIdIpAllowlist).toHaveBeenCalledWith('lic-1', body);
  });

  it('fetchLicenceStatusHistory forwards id and params', async () => {
    const page = { items: [], total: 0, limit: 0, offset: 0 };
    vi.mocked(getLicencesIdStatusHistory).mockResolvedValue({ data: page } as never);
    expect(await fetchLicenceStatusHistory('lic-1', { limit: 5, offset: 0 })).toEqual(page);
    expect(getLicencesIdStatusHistory).toHaveBeenCalledWith('lic-1', { limit: 5, offset: 0 });
  });

  it('fetchLicenceBindingHistory forwards id and params', async () => {
    const page = { items: [], total: 0, limit: 0, offset: 0 };
    vi.mocked(getLicencesIdBindingHistory).mockResolvedValue({ data: page } as never);
    expect(await fetchLicenceBindingHistory('lic-1', { limit: 25, offset: 0 })).toEqual(page);
    expect(getLicencesIdBindingHistory).toHaveBeenCalledWith('lic-1', { limit: 25, offset: 0 });
  });

  it('fetchLicenceVerificationAttempts forwards id and params', async () => {
    const page = { items: [], total: 0, limit: 0, offset: 0 };
    vi.mocked(getLicencesIdVerificationAttempts).mockResolvedValue({ data: page } as never);
    expect(await fetchLicenceVerificationAttempts('lic-1', { outcome: 'fail', limit: 10, offset: 0 })).toEqual(page);
    expect(getLicencesIdVerificationAttempts).toHaveBeenCalledWith('lic-1', { outcome: 'fail', limit: 10, offset: 0 });
  });

  it('fetchLicenceMembers forwards the id and unwraps .data', async () => {
    vi.mocked(getLicencesIdMembers).mockResolvedValue({ data: [{ id: 'm1' }] } as never);
    expect(await fetchLicenceMembers('lic-1')).toEqual([{ id: 'm1' }]);
    expect(getLicencesIdMembers).toHaveBeenCalledWith('lic-1');
  });

  it('addLicenceMember forwards id and body', async () => {
    vi.mocked(postLicencesIdMembers).mockResolvedValue({ data: { id: 'm1' } } as never);
    const body = { email: 'a@b.com' };
    expect(await addLicenceMember('lic-1', body as never)).toEqual({ id: 'm1' });
    expect(postLicencesIdMembers).toHaveBeenCalledWith('lic-1', body);
  });

  it('removeLicenceMember calls deleteLicencesIdMembersMemberId with both ids', async () => {
    vi.mocked(deleteLicencesIdMembersMemberId).mockResolvedValue({ data: undefined } as never);
    await expect(removeLicenceMember('lic-1', 'm1')).resolves.toBeUndefined();
    expect(deleteLicencesIdMembersMemberId).toHaveBeenCalledWith('lic-1', 'm1');
  });

  it('fetchLicenceSeats forwards the id and unwraps .data', async () => {
    const seats = { max: 1, live: [] };
    vi.mocked(getLicencesIdSeats).mockResolvedValue({ data: seats } as never);
    expect(await fetchLicenceSeats('lic-1')).toEqual(seats);
    expect(getLicencesIdSeats).toHaveBeenCalledWith('lic-1');
  });

  it('forceRevokeSeat calls deleteLicencesIdSeatsSeatId with both ids', async () => {
    vi.mocked(deleteLicencesIdSeatsSeatId).mockResolvedValue({ data: undefined } as never);
    await expect(forceRevokeSeat('lic-1', 'seat-1')).resolves.toBeUndefined();
    expect(deleteLicencesIdSeatsSeatId).toHaveBeenCalledWith('lic-1', 'seat-1');
  });

  it('updateLicenceMaxSeats forwards id and body', async () => {
    vi.mocked(patchLicencesIdMaxSeats).mockResolvedValue({ data: { id: 'lic-1', maxSeats: 5 } } as never);
    const body = { maxSeats: 5, reason: null };
    expect(await updateLicenceMaxSeats('lic-1', body as never)).toEqual({ id: 'lic-1', maxSeats: 5 });
    expect(patchLicencesIdMaxSeats).toHaveBeenCalledWith('lic-1', body);
  });
});
