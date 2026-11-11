/**
 * Endpoints behind the client groups store.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { ClientGroup } from '@/types/clientgroup'

/**
 * Endpoint functions for `/admin/client-groups`.
 *
 * @returns One typed function per endpoint.
 */
export const useClientGroupsApi = () => {
  const { request } = useApi()

  /**
   * Lists every client group with how many clients belong to it.
   *
   * @returns The groups.
   */
  const listGroups = (): Promise<ClientGroup[]> => request<ClientGroup[]>(ApiPath.AdminClientGroups)

  return { listGroups }
}
