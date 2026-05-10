// frontend/src/auth/api-client.ts
// Stub — replaced in Task 7. orval needs this file to exist to resolve the mutator path.

export const apiClient = async <T>(_args: {
  url: string;
  method: string;
  headers?: Record<string, string>;
  data?: unknown;
  params?: Record<string, string | number | boolean | undefined>;
  signal?: AbortSignal;
}): Promise<T> => {
  throw new Error('apiClient stub — not yet implemented');
};

export class ApiError extends Error {
  constructor(
    public status: number,
    public body: unknown,
  ) {
    super(`API error ${status}`);
  }
}
