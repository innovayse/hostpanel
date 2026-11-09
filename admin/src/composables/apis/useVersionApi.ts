/**
 * Endpoint function for the installed version resource.
 *
 * No state lives here; the version store owns it. The function rejects with an `ApiError`
 * carrying the problem+json body on failure.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { InstalledVersionDto } from '@/types/installedVersion'

/**
 * Endpoint function for the installed version.
 *
 * @returns One typed function per endpoint.
 */
export const useVersionApi = () => {
  const { request } = useApi()

  /**
   * Fetches the installed Hostpanel version; any signed-in staff role may ask.
   *
   * @returns The installed version DTO.
   */
  const getInstalledVersion = (): Promise<InstalledVersionDto> =>
    request<InstalledVersionDto>(ApiPath.AdminVersion)

  return { getInstalledVersion }
}
