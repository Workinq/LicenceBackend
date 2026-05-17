import { apiClient } from '@/auth/api-client';
import type { GetAuditEventsParams, PagedResponseOfAuditEventResponse } from './generated/api.schemas';

// We bypass the orval-generated getAuditEvents here because its URL builder serialises
// array params with .toString(), which produces a single CSV value. ASP.NET Core's
// string[] query binder expects repeated keys (event_type=a&event_type=b), so the CSV
// silently matches nothing. Build the query string by hand instead.
function buildQuery(params: GetAuditEventsParams): string {
  const sp = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null) continue;
    if (Array.isArray(value)) {
      for (const item of value) sp.append(key, String(item));
    } else {
      sp.append(key, String(value));
    }
  }
  const qs = sp.toString();
  return qs.length > 0 ? `?${qs}` : '';
}

export async function fetchAuditEvents(params: GetAuditEventsParams): Promise<PagedResponseOfAuditEventResponse> {
  const url = `/audit-events${buildQuery(params)}`;
  const res = await apiClient<{ data: PagedResponseOfAuditEventResponse; status: number; headers: Headers }>(url, { method: 'GET' });
  return res.data;
}
