/**
 * One function per endpoint on the plan change request surface
 * (`/api/admin/service-change-requests`). No state lives here, no error handling.
 *
 * Callers are the service change requests store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type { ServiceChangeRequestItem, ServiceChangeStatus } from '@/types/servicechangerequest'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for plan change requests.
 *
 * @returns One typed function per plan change request endpoint.
 */
export const useServiceChangeRequestsApi = () => {
  const { request } = useApi()

  /**
   * Lists plan change requests, newest first.
   *
   * @param status - Only requests in this status; empty for every status.
   * @param serviceId - Only the requests of this service; null for every service.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @returns One page of requests.
   */
  const list = (
    status: ServiceChangeStatus | '',
    serviceId: number | null,
    page: number,
    pageSize: number,
  ): Promise<PaginatedList<ServiceChangeRequestItem>> =>
    request<PaginatedList<ServiceChangeRequestItem>>(
      `${ApiPath.AdminServiceChangeRequests}${buildQuery({ status, serviceId, page, pageSize })}`,
    )

  /**
   * Approves a request and applies it.
   *
   * @param id - The request id.
   * @param note - Optional staff note kept with the request.
   * @returns Nothing.
   */
  const approve = (id: number, note: string | null): Promise<void> =>
    request<void>(`${ApiPath.AdminServiceChangeRequests}/${id}/approve`, { method: HttpMethod.Post, body: JSON.stringify({ note }) })

  /**
   * Rejects a pending or approved request.
   *
   * @param id - The request id.
   * @param note - Optional staff note kept with the request.
   * @returns Nothing.
   */
  const reject = (id: number, note: string | null): Promise<void> =>
    request<void>(`${ApiPath.AdminServiceChangeRequests}/${id}/reject`, { method: HttpMethod.Post, body: JSON.stringify({ note }) })

  return { list, approve, reject }
}
