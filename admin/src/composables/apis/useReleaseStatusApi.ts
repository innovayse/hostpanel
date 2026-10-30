/**
 * Endpoint function for the release status resource.
 *
 * No state lives here; the release status store owns it. The function rejects with an
 * `ApiError` carrying the problem+json body on failure.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { ReleaseStatusDto } from '@/types/releaseStatus'

/**
 * Endpoint function for the admin release status.
 *
 * @returns One typed function per endpoint.
 */
export const useReleaseStatusApi = () => {
  const { request } = useApi()

  /**
   * Fetches the installed version, the latest cached GitHub release, and the upgrade status.
   * No outbound HTTP call is made on the backend for this request.
   *
   * @returns The release status DTO.
   */
  const getReleaseStatus = (): Promise<ReleaseStatusDto> =>
    request<ReleaseStatusDto>(ApiPath.AdminReleaseStatus)

  return { getReleaseStatus }
}
