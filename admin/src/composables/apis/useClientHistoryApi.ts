/**
 * One function per history endpoint of a client profile: the activity log
 * (`/api/clients/{id}/activity-logs`) and the e-mail log (`/api/admin/email-logs/client/{id}`).
 * No state lives here, no error handling.
 *
 * Callers are the client log and client email stores, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { ActivityLog, ActivityLogFilters } from '@/types/activitylog'
import type { EmailLog, EmailLogFilters } from '@/types/emaillog'
import type { PaginatedList } from '@/types/paginatedlist'
import { ApiPath } from '@/lib/apiPaths'

/**
 * Endpoint functions for the client history views.
 *
 * @returns One typed function per history endpoint.
 */
export const useClientHistoryApi = () => {
  const { request } = useApi()

  /**
   * Lists the activity log of a client, one page.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @param filters - The search text and the period; a member left out does not narrow.
   * @returns One page of entries.
   */
  const activityLogs = (
    clientId: number,
    page: number,
    pageSize: number,
    filters: ActivityLogFilters = {},
  ): Promise<PaginatedList<ActivityLog>> =>
    request<PaginatedList<ActivityLog>>(
      `${ApiPath.Clients}/${clientId}/activity-logs${buildQuery({ page, pageSize, search: filters.search, from: filters.from, to: filters.to })}`,
    )

  /**
   * Lists the e-mails sent to a client, one page.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @param filters - The search text, the outcome and the period; a member left out does not narrow.
   * @returns One page of entries.
   */
  const emailLogs = (
    clientId: number,
    page: number,
    pageSize: number,
    filters: EmailLogFilters = {},
  ): Promise<PaginatedList<EmailLog>> =>
    request<PaginatedList<EmailLog>>(
      `${ApiPath.AdminEmailLogs}/client/${clientId}${buildQuery({
        page,
        pageSize,
        search: filters.search,
        success: filters.success,
        from: filters.from,
        to: filters.to,
      })}`,
    )

  return { activityLogs, emailLogs }
}
