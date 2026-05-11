import { getProducts, postProducts } from './generated/api';
import type { PagedResponseOfProductResponse, CreateProductRequest, ProductResponse } from './generated/api.schemas';

export async function fetchProducts(): Promise<PagedResponseOfProductResponse> {
  return (await getProducts({ limit: 200, offset: 0 })).data as PagedResponseOfProductResponse;
}

export async function createProduct(body: CreateProductRequest): Promise<ProductResponse> {
  return (await postProducts(body)).data as ProductResponse;
}
