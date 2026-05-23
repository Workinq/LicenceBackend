import {
  deleteLicencesIdKeysKeyId,
  getLicencesIdKeys,
  patchLicencesIdKeysKeyId,
  postLicencesIdKeys,
} from './generated/api';
import type {
  GetLicencesIdKeysParams,
  LicenceKeyMintedResponse,
  LicenceKeyResponse,
  LicenceKeysResponse,
  MintLicenceKeyRequest,
  RevokeLicenceKeyRequest,
  UpdateLicenceKeyLabelRequest,
} from './generated/api.schemas';

export async function fetchLicenceKeys(
  licenceId: string,
  params: GetLicencesIdKeysParams = {},
): Promise<LicenceKeysResponse> {
  return (await getLicencesIdKeys(licenceId, params)).data as LicenceKeysResponse;
}

export async function mintLicenceKey(
  licenceId: string,
  body: MintLicenceKeyRequest,
): Promise<LicenceKeyMintedResponse> {
  return (await postLicencesIdKeys(licenceId, body)).data as LicenceKeyMintedResponse;
}

export async function revokeLicenceKey(
  licenceId: string,
  keyId: string,
  body: RevokeLicenceKeyRequest = null,
): Promise<LicenceKeyResponse> {
  return (await deleteLicencesIdKeysKeyId(licenceId, keyId, body)).data as LicenceKeyResponse;
}

export async function updateLicenceKeyLabel(
  licenceId: string,
  keyId: string,
  body: UpdateLicenceKeyLabelRequest,
): Promise<LicenceKeyResponse> {
  return (await patchLicencesIdKeysKeyId(licenceId, keyId, body)).data as LicenceKeyResponse;
}
