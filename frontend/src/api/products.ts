import { getProducts } from './generated/api';
import type { PagedResponseOfProductResponse } from './generated/api.schemas';

export async function fetchProducts(): Promise<PagedResponseOfProductResponse> {
  return (await getProducts({ limit: 200, offset: 0 })).data as PagedResponseOfProductResponse;
}
