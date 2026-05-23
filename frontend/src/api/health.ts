import { authedFetch } from '@/auth/api-client';

export interface HealthResponse {
  status: string;
  db: string;
  version: string;
}

export async function fetchHealth(): Promise<HealthResponse> {
  const response = await authedFetch('/health');
  if (!response.ok && response.status !== 503) {
    throw new Error(`Health check failed: ${response.status}`);
  }
  return (await response.json()) as HealthResponse;
}
