import { getUsers, getUsersId, getUsersIdLicences, patchUsersIdStatus, postUsers } from './generated/api';
import type {
  CreateUserRequest,
  GetUsersIdLicencesParams,
  GetUsersParams,
  PagedResponseOfLicenceResponse,
  PagedResponseOfUserResponse,
  UpdateUserStatusRequest,
  UserResponse,
} from './generated/api.schemas';

const DEFAULT_USERS_PARAMS: GetUsersParams = { limit: 200, offset: 0 };

export async function fetchUsers(params: GetUsersParams = DEFAULT_USERS_PARAMS): Promise<PagedResponseOfUserResponse> {
  return (await getUsers(params)).data as PagedResponseOfUserResponse;
}

export async function fetchUser(id: string): Promise<UserResponse> {
  return (await getUsersId(id)).data as UserResponse;
}

export async function createUser(body: CreateUserRequest): Promise<UserResponse> {
  return (await postUsers(body)).data as UserResponse;
}

export async function updateUserStatus(id: string, body: UpdateUserStatusRequest): Promise<UserResponse> {
  return (await patchUsersIdStatus(id, body)).data as UserResponse;
}

export async function fetchUserLicences(
  id: string,
  params: GetUsersIdLicencesParams = {},
): Promise<PagedResponseOfLicenceResponse> {
  return (await getUsersIdLicences(id, params)).data as PagedResponseOfLicenceResponse;
}
