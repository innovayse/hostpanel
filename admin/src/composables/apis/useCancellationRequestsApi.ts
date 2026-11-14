/**
 * One function per endpoint on the cancellation-request surface
 * (`/api/admin/cancellation-requests`). No state lives here, no error handling.
 *
 * Callers are the cancellation requests store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type { CancellationRequestFilters, CancellationRequestItem } from '@/types/service'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for cancellation requests.
 *
 * @returns One typed function per cancellation-request endpoint.
 */
export const useCancellationRequestsApi = () => {
  const { request } = useApi()

  /**
   * Lists cancellation requests.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text, type and status filters; empty for none.
   * @returns One page of requests and the total count of matches.
   */
  const list = (page: number, pageSize: number, filters: CancellationRequestFilters = {}): Promise<PaginatedList<CancellationRequestItem>> =>
    request<PaginatedList<CancellationRequestItem>>(
      `${ApiPath.AdminCancellationRequests}${buildQuery({ page, pageSize, ...filters })}`,
    )

  /**
   * Deletes a cancellation request.
   *
   * @param id - The request id.
   * @returns Nothing.
   */
  const remove = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminCancellationRequests}/${id}`, { method: HttpMethod.Delete })

  return { list, remove }
}
