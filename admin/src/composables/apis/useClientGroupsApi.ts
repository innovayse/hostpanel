/**
 * Endpoints behind the client groups store.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import type { ClientGroup, ClientGroupPayload } from '@/types/clientgroup'
import { HttpMethod } from '@/types/http'

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

  /**
   * Creates a client group; a new group is never the default.
   *
   * @param payload - The group's fields.
   * @returns The new group's id.
   */
  const createGroup = (payload: ClientGroupPayload): Promise<number> =>
    request<number>(ApiPath.AdminClientGroups, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a group's name, description, discount and colour.
   *
   * @param id - The group id.
   * @param payload - The group's new fields.
   * @returns Nothing.
   */
  const updateGroup = (id: number, payload: ClientGroupPayload): Promise<void> =>
    request<void>(`${ApiPath.AdminClientGroups}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Makes a group the default for new clients, or clears the flag.
   *
   * @param id - The group id.
   * @param isDefault - True to make it the default; false to clear the flag.
   * @returns Nothing.
   */
  const setDefaultGroup = (id: number, isDefault: boolean): Promise<void> =>
    request<void>(`${ApiPath.AdminClientGroups}/${id}/default`, {
      method: HttpMethod.Put,
      body: JSON.stringify({ isDefault }),
    })

  /**
   * Deletes a group that has no clients.
   *
   * @param id - The group id.
   * @returns Nothing.
   */
  const removeGroup = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminClientGroups}/${id}`, { method: HttpMethod.Delete })

  return { listGroups, createGroup, updateGroup, setDefaultGroup, removeGroup }
}
