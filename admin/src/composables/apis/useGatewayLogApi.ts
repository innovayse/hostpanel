/**
 * One function per endpoint on the API payment-gateway log surface (`/api/gateway-logs`). No
 * state lives here, no error handling.
 *
 * The caller is the gateway log store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type { GatewayLogEntry, GatewayLogFilters } from '@/types/gatewaylog'

/**
 * Endpoint functions for the gateway log.
 *
 * @returns One typed function per gateway log endpoint the admin panel uses.
 */
export const useGatewayLogApi = () => {
  const { request } = useApi()

  /**
   * Lists one page of the gateway log, newest first.
   *
   * @param filters - The search text, the period as bare days and the result; a member left out does not narrow.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @returns One page of log entries.
   */
  const list = (filters: GatewayLogFilters, page: number, pageSize: number): Promise<PaginatedList<GatewayLogEntry>> =>
    request<PaginatedList<GatewayLogEntry>>(
      `/gateway-logs${buildQuery({
        page,
        pageSize,
        search: filters.search,
        from: filters.from,
        to: filters.to,
        result: filters.result,
      })}`,
    )

  return { list }
}
