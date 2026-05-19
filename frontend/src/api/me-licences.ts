import {
  deleteMeLicencesIdMembersMemberId,
  getMeLicences,
  getMeLicencesId,
  getMeLicencesIdMembers,
  patchMeLicencesIdLabel,
  postMeLicencesIdMembers,
  postMeLicencesIdRegenerateKey,
} from './generated/api';
import { downloadBlob, type DownloadedBlob } from './product-files';
import type {
  AddLicenceMemberRequest,
  GetMeLicencesParams,
  LicenceKeyRegeneratedResponse,
  LicenceMemberResponse,
  LicenceResponse,
  PagedResponseOfLicenceResponse,
  RegenerateLicenceKeyRequest,
  UpdateLicenceLabelRequest,
} from './generated/api.schemas';

export async function fetchMyLicences(params: GetMeLicencesParams = {}): Promise<PagedResponseOfLicenceResponse> {
  return (await getMeLicences(params)).data as PagedResponseOfLicenceResponse;
}

export async function fetchMyLicence(id: string): Promise<LicenceResponse> {
  return (await getMeLicencesId(id)).data as LicenceResponse;
}

export async function fetchMyLicenceMembers(id: string): Promise<LicenceMemberResponse[]> {
  return (await getMeLicencesIdMembers(id)).data as LicenceMemberResponse[];
}

export async function addMyLicenceMember(id: string, body: AddLicenceMemberRequest): Promise<LicenceMemberResponse> {
  return (await postMeLicencesIdMembers(id, body)).data as LicenceMemberResponse;
}

export async function removeMyLicenceMember(id: string, memberId: string): Promise<void> {
  await deleteMeLicencesIdMembersMemberId(id, memberId);
}

export async function regenerateMyLicenceKey(
  id: string,
  body: RegenerateLicenceKeyRequest,
): Promise<LicenceKeyRegeneratedResponse> {
  return (await postMeLicencesIdRegenerateKey(id, body)).data as LicenceKeyRegeneratedResponse;
}

export async function updateMyLicenceLabel(
  id: string,
  body: UpdateLicenceLabelRequest,
): Promise<LicenceResponse> {
  return (await patchMeLicencesIdLabel(id, body)).data as LicenceResponse;
}

export async function downloadMyLicenceFile(id: string): Promise<DownloadedBlob> {
  return downloadBlob(`/me/licences/${id}/download`);
}
