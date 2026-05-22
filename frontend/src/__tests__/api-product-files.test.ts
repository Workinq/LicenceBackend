import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('../api/generated/api', () => ({
  getProductsIdFiles: vi.fn(),
  postProductsIdFiles: vi.fn(),
}));

vi.mock('@/auth/api-client', () => ({
  authedFetch: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    body: unknown;
    constructor(status: number, body: unknown) {
      super(`API error ${status}`);
      this.status = status;
      this.body = body;
    }
  },
}));

import { getProductsIdFiles, postProductsIdFiles } from '../api/generated/api';
import { authedFetch, ApiError } from '@/auth/api-client';
import {
  fetchProductFiles,
  uploadProductFile,
  downloadProductFileRevision,
  downloadBlob,
  triggerBlobDownload,
} from '../api/product-files';

function blobResponse(status: number, body: BodyInit | null, headers?: Record<string, string>): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(headers ?? {}),
    blob: async () => (body instanceof Blob ? body : new Blob([body ?? ''])),
    json: async () => { throw new Error('not json'); },
  } as unknown as Response;
}

beforeEach(() => {
  vi.mocked(getProductsIdFiles).mockReset();
  vi.mocked(postProductsIdFiles).mockReset();
  vi.mocked(authedFetch).mockReset();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('api/product-files', () => {
  it('fetchProductFiles calls getProductsIdFiles and unwraps .data', async () => {
    const files = [{ id: 'f1', fileName: 'a.zip' }];
    vi.mocked(getProductsIdFiles).mockResolvedValue({ data: files } as never);
    expect(await fetchProductFiles('p1')).toEqual(files);
    expect(getProductsIdFiles).toHaveBeenCalledWith('p1');
  });

  it('uploadProductFile wraps the file in an object and forwards to postProductsIdFiles', async () => {
    const file = new File(['hello'], 'hello.txt', { type: 'text/plain' });
    const created = { id: 'f1', fileName: 'hello.txt' };
    vi.mocked(postProductsIdFiles).mockResolvedValue({ data: created } as never);
    expect(await uploadProductFile('p1', file)).toEqual(created);
    expect(postProductsIdFiles).toHaveBeenCalledWith('p1', { file });
  });

  it('downloadProductFileRevision routes through authedFetch with the canonical path', async () => {
    const blob = new Blob(['payload'], { type: 'application/octet-stream' });
    vi.mocked(authedFetch).mockResolvedValue(blobResponse(200, blob, { 'Content-Disposition': 'attachment; filename="release.zip"' }));
    const dl = await downloadProductFileRevision('p1', 'f1');
    expect(dl.fileName).toBe('release.zip');
    expect(dl.blob).toBeInstanceOf(Blob);
    expect(authedFetch).toHaveBeenCalledWith('/products/p1/files/f1/download');
  });

  it('downloadBlob parses a filename* UTF-8 header', async () => {
    vi.mocked(authedFetch).mockResolvedValue(blobResponse(200, 'data', { 'Content-Disposition': "attachment; filename*=UTF-8''release%20notes.txt" }));
    const dl = await downloadBlob('/x');
    expect(dl.fileName).toBe('release notes.txt');
  });

  it('downloadBlob parses a plain filename header when filename* is absent', async () => {
    vi.mocked(authedFetch).mockResolvedValue(blobResponse(200, 'data', { 'Content-Disposition': 'attachment; filename="simple.bin"' }));
    const dl = await downloadBlob('/x');
    expect(dl.fileName).toBe('simple.bin');
  });

  it('downloadBlob returns a null filename when no Content-Disposition is present', async () => {
    vi.mocked(authedFetch).mockResolvedValue(blobResponse(200, 'data'));
    const dl = await downloadBlob('/y');
    expect(dl.fileName).toBeNull();
  });

  it('downloadBlob throws an ApiError when the response is not ok', async () => {
    vi.mocked(authedFetch).mockResolvedValue({
      ok: false,
      status: 404,
      headers: new Headers(),
      blob: async () => new Blob([]),
      json: async () => ({ error: 'missing' }),
    } as unknown as Response);
    const err = await downloadBlob('/missing').catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(404);
    expect((err as ApiError).body).toEqual({ error: 'missing' });
  });

  it('downloadBlob throws ApiError with null body when the error is not json', async () => {
    vi.mocked(authedFetch).mockResolvedValue({
      ok: false,
      status: 500,
      headers: new Headers(),
      blob: async () => new Blob([]),
      json: async () => { throw new Error('binary'); },
    } as unknown as Response);
    const err = await downloadBlob('/err').catch((e: unknown) => e);
    expect((err as ApiError).status).toBe(500);
    expect((err as ApiError).body).toBeNull();
  });

  it('triggerBlobDownload creates an anchor with the given filename and revokes the object URL', () => {
    const createObjectURL = vi.fn(() => 'blob:fake');
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true });
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    triggerBlobDownload({ blob: new Blob(['x']), fileName: 'named.bin' }, 'fallback.bin');
    expect(createObjectURL).toHaveBeenCalled();
    expect(clickSpy).toHaveBeenCalled();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:fake');
  });

  it('triggerBlobDownload falls back to the provided name when fileName is null', () => {
    const createObjectURL = vi.fn(() => 'blob:fake');
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true });
    const clickSpy = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      expect(this.download).toBe('fallback.bin');
    });
    triggerBlobDownload({ blob: new Blob(['x']), fileName: null }, 'fallback.bin');
    expect(clickSpy).toHaveBeenCalled();
  });
});
