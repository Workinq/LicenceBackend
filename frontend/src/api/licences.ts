import { getLicences, getLicencesId, postLicences } from './generated/api';
import type {
  CreateLicenceRequest,
  GetLicencesParams,
  LicenceCreatedResponse,
  LicenceResponse,
  PagedResponseOfLicenceResponse,
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
