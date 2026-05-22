import {
  postPaymentsCheckout,
  getPaymentsCheckoutId,
  getPaymentsConfig,
} from './generated/api';
import type {
  CreateOrderRequest,
  CheckoutSessionResponse,
  CheckoutStatusResponse,
  PaymentConfigResponse,
} from './generated/api.schemas';

export async function startCheckout(body: CreateOrderRequest): Promise<CheckoutSessionResponse> {
  return (await postPaymentsCheckout(body)).data as CheckoutSessionResponse;
}

export async function fetchCheckoutStatus(id: string): Promise<CheckoutStatusResponse> {
  return (await getPaymentsCheckoutId(id)).data as CheckoutStatusResponse;
}

export async function fetchPaymentConfig(): Promise<PaymentConfigResponse> {
  return (await getPaymentsConfig()).data as PaymentConfigResponse;
}
