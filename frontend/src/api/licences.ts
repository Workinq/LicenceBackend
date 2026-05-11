import {
  getLicences,
  getLicencesId,
  getLicencesIdBindingHistory,
  getLicencesIdStatusHistory,
  getLicencesIdVerificationAttempts,
  patchLicencesIdStatus,
  postLicences,
  putLicencesIdHwid,
  putLicencesIdIpAllowlist,
} from './generated/api';
import type {
  CreateLicenceRequest,
  GetLicencesParams,
  LicenceCreatedResponse,
  LicenceResponse,
  PagedResponseOfBindingHistoryEntryResponse,
  PagedResponseOfLicenceResponse,
  PagedResponseOfLicenceStatusHistoryResponse,
  PagedResponseOfVerificationAttemptResponse,
  UpdateLicenceHwidRequest,
  UpdateLicenceIpAllowlistRequest,
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
