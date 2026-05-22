import {
  getMeOrders,
  getMeOrdersId,
  getAdminOrders,
  getAdminOrdersId,
} from './generated/api';
import type {
  GetAdminOrdersParams,
  GetMeOrdersParams,
  OrderResponse,
  PagedResponseOfOrderResponse,
} from './generated/api.schemas';

export async function fetchMyOrders(params: GetMeOrdersParams = {}): Promise<PagedResponseOfOrderResponse> {
  return (await getMeOrders(params)).data as PagedResponseOfOrderResponse;
}

export async function fetchMyOrder(id: string): Promise<OrderResponse> {
  return (await getMeOrdersId(id)).data as OrderResponse;
}

export async function fetchAdminOrders(params: GetAdminOrdersParams = {}): Promise<PagedResponseOfOrderResponse> {
  return (await getAdminOrders(params)).data as PagedResponseOfOrderResponse;
}

export async function fetchAdminOrder(id: string): Promise<OrderResponse> {
  return (await getAdminOrdersId(id)).data as OrderResponse;
}
