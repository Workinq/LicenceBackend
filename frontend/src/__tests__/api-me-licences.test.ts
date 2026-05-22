import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  deleteMeLicencesIdMembersMemberId: vi.fn(),
  getMeLicences: vi.fn(),
  getMeLicencesId: vi.fn(),
  getMeLicencesIdMembers: vi.fn(),
  getMeLicencesIdSeats: vi.fn(),
  patchMeLicencesIdLabel: vi.fn(),
  postMeLicencesIdMembers: vi.fn(),
  postMeLicencesIdRegenerateKey: vi.fn(),
}));

vi.mock('../api/product-files', () => ({
  downloadBlob: vi.fn(),
}));

import {
  deleteMeLicencesIdMembersMemberId,
  getMeLicences,
  getMeLicencesId,
  getMeLicencesIdMembers,
  getMeLicencesIdSeats,
  patchMeLicencesIdLabel,
  postMeLicencesIdMembers,
  postMeLicencesIdRegenerateKey,
} from '../api/generated/api';
import { downloadBlob } from '../api/product-files';
import {
  addMyLicenceMember,
  downloadMyLicenceFile,
  fetchMyLicence,
  fetchMyLicenceMembers,
  fetchMyLicenceSeats,
  fetchMyLicences,
  regenerateMyLicenceKey,
  removeMyLicenceMember,
  updateMyLicenceLabel,
} from '../api/me-licences';

beforeEach(() => {
  vi.mocked(deleteMeLicencesIdMembersMemberId).mockReset();
  vi.mocked(getMeLicences).mockReset();
  vi.mocked(getMeLicencesId).mockReset();
  vi.mocked(getMeLicencesIdMembers).mockReset();
  vi.mocked(getMeLicencesIdSeats).mockReset();
  vi.mocked(patchMeLicencesIdLabel).mockReset();
  vi.mocked(postMeLicencesIdMembers).mockReset();
  vi.mocked(postMeLicencesIdRegenerateKey).mockReset();
  vi.mocked(downloadBlob).mockReset();
});

describe('api/me-licences', () => {
  it('fetchMyLicences forwards params and unwraps .data', async () => {
    const params = { limit: 10, offset: 0 };
    const page = { items: [], total: 0, limit: 10, offset: 0 };
    vi.mocked(getMeLicences).mockResolvedValue({ data: page } as never);
    expect(await fetchMyLicences(params as never)).toEqual(page);
    expect(getMeLicences).toHaveBeenCalledWith(params);
  });

  it('fetchMyLicences defaults params to an empty object', async () => {
    vi.mocked(getMeLicences).mockResolvedValue({ data: { items: [], total: 0, limit: 0, offset: 0 } } as never);
    await fetchMyLicences();
    expect(getMeLicences).toHaveBeenCalledWith({});
  });

  it('fetchMyLicence calls getMeLicencesId with the id', async () => {
    vi.mocked(getMeLicencesId).mockResolvedValue({ data: { id: 'l1' } } as never);
    expect(await fetchMyLicence('l1')).toEqual({ id: 'l1' });
    expect(getMeLicencesId).toHaveBeenCalledWith('l1');
  });

  it('fetchMyLicenceMembers calls getMeLicencesIdMembers with the id', async () => {
    const members = [{ id: 'm1' }];
    vi.mocked(getMeLicencesIdMembers).mockResolvedValue({ data: members } as never);
    expect(await fetchMyLicenceMembers('l1')).toEqual(members);
    expect(getMeLicencesIdMembers).toHaveBeenCalledWith('l1');
  });

  it('addMyLicenceMember forwards id and body', async () => {
    const body = { email: 'a@b.com' };
    vi.mocked(postMeLicencesIdMembers).mockResolvedValue({ data: { id: 'm1' } } as never);
    expect(await addMyLicenceMember('l1', body as never)).toEqual({ id: 'm1' });
    expect(postMeLicencesIdMembers).toHaveBeenCalledWith('l1', body);
  });

  it('removeMyLicenceMember forwards id and memberId', async () => {
    vi.mocked(deleteMeLicencesIdMembersMemberId).mockResolvedValue({ data: undefined } as never);
    await expect(removeMyLicenceMember('l1', 'm1')).resolves.toBeUndefined();
    expect(deleteMeLicencesIdMembersMemberId).toHaveBeenCalledWith('l1', 'm1');
  });

  it('regenerateMyLicenceKey forwards id and body', async () => {
    const body = { reason: 'lost' };
    vi.mocked(postMeLicencesIdRegenerateKey).mockResolvedValue({ data: { key: 'abc' } } as never);
    expect(await regenerateMyLicenceKey('l1', body as never)).toEqual({ key: 'abc' });
    expect(postMeLicencesIdRegenerateKey).toHaveBeenCalledWith('l1', body);
  });

  it('updateMyLicenceLabel forwards id and body', async () => {
    const body = { label: 'My Mac' };
    vi.mocked(patchMeLicencesIdLabel).mockResolvedValue({ data: { id: 'l1', label: 'My Mac' } } as never);
    expect(await updateMyLicenceLabel('l1', body as never)).toEqual({ id: 'l1', label: 'My Mac' });
    expect(patchMeLicencesIdLabel).toHaveBeenCalledWith('l1', body);
  });

  it('downloadMyLicenceFile calls downloadBlob with the canonical path', async () => {
    const dl = { blob: new Blob(['x']), fileName: 'file.bin' };
    vi.mocked(downloadBlob).mockResolvedValue(dl);
    expect(await downloadMyLicenceFile('l1')).toBe(dl);
    expect(downloadBlob).toHaveBeenCalledWith('/me/licences/l1/download');
  });

  it('fetchMyLicenceSeats calls getMeLicencesIdSeats with the id', async () => {
    const seats = { active: 1, total: 5 };
    vi.mocked(getMeLicencesIdSeats).mockResolvedValue({ data: seats } as never);
    expect(await fetchMyLicenceSeats('l1')).toEqual(seats);
    expect(getMeLicencesIdSeats).toHaveBeenCalledWith('l1');
  });
});
