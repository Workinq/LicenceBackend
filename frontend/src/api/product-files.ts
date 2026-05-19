import { getProductsIdFiles, postProductsIdFiles } from './generated/api';
import type { ProductFileResponse } from './generated/api.schemas';
import { authedFetch, ApiError } from '@/auth/api-client';

export async function fetchProductFiles(productId: string): Promise<ProductFileResponse[]> {
  return (await getProductsIdFiles(productId)).data as ProductFileResponse[];
}

export async function uploadProductFile(productId: string, file: File): Promise<ProductFileResponse> {
  return (await postProductsIdFiles(productId, { file })).data as ProductFileResponse;
}

export async function downloadProductFileRevision(productId: string, fileId: string): Promise<DownloadedBlob> {
  return downloadBlob(`/products/${productId}/files/${fileId}/download`);
}

export interface DownloadedBlob {
  blob: Blob;
  fileName: string | null;
}

export async function downloadBlob(path: string): Promise<DownloadedBlob> {
  const response = await authedFetch(path);
  if (!response.ok) {
    let body: unknown = null;
    try { body = await response.json(); } catch { /* binary or empty */ }
    throw new ApiError(response.status, body);
  }
  const blob = await response.blob();
  const disposition = response.headers.get('Content-Disposition');
  return { blob, fileName: parseContentDispositionFilename(disposition) };
}

export function triggerBlobDownload({ blob, fileName }: DownloadedBlob, fallbackName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName ?? fallbackName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

function parseContentDispositionFilename(header: string | null): string | null {
  if (!header) return null;
  const star = /filename\*\s*=\s*(?:UTF-8'')?([^;]+)/i.exec(header);
  if (star) {
    try { return decodeURIComponent(star[1].trim().replace(/^"|"$/g, '')); } catch { /* fall through */ }
  }
  const plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
  return plain ? plain[1].trim() : null;
}
