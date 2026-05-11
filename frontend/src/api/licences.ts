import {
  getLicences,
  getLicencesId,
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
  PagedResponseOfLicenceResponse,
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
