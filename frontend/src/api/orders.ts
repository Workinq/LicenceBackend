import {
  postOrders,
  getMeOrders,
  getMeOrdersId,
  getAdminOrders,
  getAdminOrdersId,
} from './generated/api';
import type {
  CreateOrderRequest,
  GetAdminOrdersParams,
  GetMeOrdersParams,
  OrderCreatedResponse,
  OrderResponse,
  PagedResponseOfOrderResponse,
} from './generated/api.schemas';

export async function placeOrder(body: CreateOrderRequest): Promise<OrderCreatedResponse> {
  return (await postOrders(body)).data as OrderCreatedResponse;
}

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
