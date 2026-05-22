import { getMeOrdersIdInvoice, getAdminOrdersIdInvoice } from './generated/api';
import type { InvoiceResponse } from './generated/api.schemas';

export async function fetchMyInvoice(orderId: string): Promise<InvoiceResponse> {
  return (await getMeOrdersIdInvoice(orderId)).data as InvoiceResponse;
}

export async function fetchAdminInvoice(orderId: string): Promise<InvoiceResponse> {
  return (await getAdminOrdersIdInvoice(orderId)).data as InvoiceResponse;
}
