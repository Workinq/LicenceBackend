import { getProducts, postProducts, getProductsId, patchProductsId, postProductsIdImage, deleteProductsIdImage } from './generated/api';
import type { GetProductsParams, PagedResponseOfProductResponse, CreateProductRequest, ProductResponse, UpdateProductRequest } from './generated/api.schemas';

export async function fetchProducts(params: GetProductsParams = {}): Promise<PagedResponseOfProductResponse> {
  return (await getProducts(params)).data as PagedResponseOfProductResponse;
}

export async function createProduct(body: CreateProductRequest): Promise<ProductResponse> {
  return (await postProducts(body)).data as ProductResponse;
}

export async function fetchProduct(id: string): Promise<ProductResponse> {
  return (await getProductsId(id)).data as ProductResponse;
}

export async function updateProduct(id: string, body: UpdateProductRequest): Promise<ProductResponse> {
  return (await patchProductsId(id, body)).data as ProductResponse;
}

export async function uploadProductImage(id: string, file: File): Promise<ProductResponse> {
  return (await postProductsIdImage(id, { file })).data as ProductResponse;
}

export async function deleteProductImage(id: string): Promise<ProductResponse> {
  return (await deleteProductsIdImage(id)).data as ProductResponse;
}
