import { getUsers } from './generated/api';
import type { PagedResponseOfUserResponse } from './generated/api.schemas';

export async function fetchUsers(): Promise<PagedResponseOfUserResponse> {
  return (await getUsers({ limit: 200, offset: 0 })).data as PagedResponseOfUserResponse;
}
