import { postProductsIdContentImages } from './generated/api';
import type { ProductContentImageResponse } from './generated/api.schemas';

export async function uploadProductContentImage(
  productId: string,
  file: File,
): Promise<ProductContentImageResponse> {
  return (await postProductsIdContentImages(productId, { file })).data as ProductContentImageResponse;
}
