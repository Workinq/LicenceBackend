import { getMe, patchMe, patchMePassword } from './generated/api';
import type { ChangePasswordRequest, UpdateProfileRequest, UserResponse } from './generated/api.schemas';

export async function fetchMe(): Promise<UserResponse> {
  return (await getMe()).data as UserResponse;
}

export async function updateProfile(body: UpdateProfileRequest): Promise<UserResponse> {
  return (await patchMe(body)).data as UserResponse;
}

export async function changePassword(body: ChangePasswordRequest): Promise<void> {
  await patchMePassword(body);
}
