import { getUsers, postUsers } from './generated/api';
import type {
  CreateUserRequest,
  PagedResponseOfUserResponse,
  UserResponse,
} from './generated/api.schemas';

export async function fetchUsers(): Promise<PagedResponseOfUserResponse> {
  return (await getUsers({ limit: 200, offset: 0 })).data as PagedResponseOfUserResponse;
}

export async function createUser(body: CreateUserRequest): Promise<UserResponse> {
  return (await postUsers(body)).data as UserResponse;
}
