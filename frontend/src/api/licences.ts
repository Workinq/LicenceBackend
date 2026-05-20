import {
  deleteLicencesIdMembersMemberId,
  deleteLicencesIdSeatsSeatId,
  getLicences,
  getLicencesId,
  getLicencesIdBindingHistory,
  getLicencesIdMembers,
  getLicencesIdSeats,
  getLicencesIdStatusHistory,
  getLicencesIdVerificationAttempts,
  patchLicencesIdMaxSeats,
  patchLicencesIdStatus,
  postLicences,
  postLicencesIdMembers,
  postLicencesIdRegenerateKey,
  putLicencesIdHwid,
  putLicencesIdIpAllowlist,
} from './generated/api';
import type {
  AddLicenceMemberRequest,
  CreateLicenceRequest,
  GetLicencesParams,
  LicenceCreatedResponse,
  LicenceKeyRegeneratedResponse,
  LicenceMemberResponse,
  LicenceResponse,
  LicenceSeatsResponse,
  PagedResponseOfBindingHistoryEntryResponse,
  PagedResponseOfLicenceResponse,
  PagedResponseOfLicenceStatusHistoryResponse,
  PagedResponseOfVerificationAttemptResponse,
  RegenerateLicenceKeyRequest,
  UpdateLicenceHwidRequest,
  UpdateLicenceIpAllowlistRequest,
  UpdateLicenceMaxSeatsRequest,
  UpdateLicenceStatusRequest,
} from './generated/api.schemas';

// apiClient throws ApiError on non-2xx, so generated calls only resolve to their 2xx variant;
// the cast discards the unreachable error union members.
export async function fetchLicences(
  params: GetLicencesParams,
): Promise<PagedResponseOfLicenceResponse> {
  return (await getLicences(params)).data as PagedResponseOfLicenceResponse;
}

export async function fetchLicence(id: string): Promise<LicenceResponse> {
  return (await getLicencesId(id)).data as LicenceResponse;
}

export async function createLicence(body: CreateLicenceRequest): Promise<LicenceCreatedResponse> {
  return (await postLicences(body)).data as LicenceCreatedResponse;
}

export async function updateLicenceStatus(
  id: string,
  body: UpdateLicenceStatusRequest,
): Promise<LicenceResponse> {
  return (await patchLicencesIdStatus(id, body)).data as LicenceResponse;
}

export async function regenerateLicenceKey(
  id: string,
  body: RegenerateLicenceKeyRequest,
): Promise<LicenceKeyRegeneratedResponse> {
  return (await postLicencesIdRegenerateKey(id, body)).data as LicenceKeyRegeneratedResponse;
}

// These two endpoints return 204 No Content, so they resolve to void.
export async function updateLicenceHwid(id: string, body: UpdateLicenceHwidRequest): Promise<void> {
  await putLicencesIdHwid(id, body);
}

export async function updateLicenceIpAllowlist(
  id: string,
  body: UpdateLicenceIpAllowlistRequest,
): Promise<void> {
  await putLicencesIdIpAllowlist(id, body);
}

export async function fetchLicenceStatusHistory(
  id: string,
  params: { limit?: number; offset?: number },
): Promise<PagedResponseOfLicenceStatusHistoryResponse> {
  return (await getLicencesIdStatusHistory(id, params)).data as PagedResponseOfLicenceStatusHistoryResponse;
}

export async function fetchLicenceBindingHistory(
  id: string,
  params: { limit?: number; offset?: number },
): Promise<PagedResponseOfBindingHistoryEntryResponse> {
  return (await getLicencesIdBindingHistory(id, params)).data as PagedResponseOfBindingHistoryEntryResponse;
}

export async function fetchLicenceVerificationAttempts(
  id: string,
  params: { outcome?: string; limit?: number; offset?: number },
): Promise<PagedResponseOfVerificationAttemptResponse> {
  return (await getLicencesIdVerificationAttempts(id, params)).data as PagedResponseOfVerificationAttemptResponse;
}

export async function fetchLicenceMembers(id: string): Promise<LicenceMemberResponse[]> {
  return (await getLicencesIdMembers(id)).data as LicenceMemberResponse[];
}

export async function addLicenceMember(id: string, body: AddLicenceMemberRequest): Promise<LicenceMemberResponse> {
  return (await postLicencesIdMembers(id, body)).data as LicenceMemberResponse;
}

export async function removeLicenceMember(id: string, memberId: string): Promise<void> {
  await deleteLicencesIdMembersMemberId(id, memberId);
}

export async function fetchLicenceSeats(id: string): Promise<LicenceSeatsResponse> {
  return (await getLicencesIdSeats(id)).data as LicenceSeatsResponse;
}

export async function forceRevokeSeat(licenceId: string, seatId: string): Promise<void> {
  await deleteLicencesIdSeatsSeatId(licenceId, seatId);
}

export async function updateLicenceMaxSeats(
  licenceId: string,
  body: UpdateLicenceMaxSeatsRequest,
): Promise<LicenceResponse> {
  return (await patchLicencesIdMaxSeats(licenceId, body)).data as LicenceResponse;
}
