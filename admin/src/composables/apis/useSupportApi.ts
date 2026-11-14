/**
 * Endpoints behind the support store: tickets, their replies, departments and the overview.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { Department } from '@/types/department'
import type { PaginatedList } from '@/types/paginatedlist'
import type { SupportOverviewPeriod, SupportOverviewStats } from '@/types/supportoverviewstats'
import type {
  CreateTicketPayload,
  Ticket,
  TicketBulkPayload,
  TicketFilter,
  TicketListItem,
  TicketReplyPayload,
  UpdateTicketPayload,
} from '@/types/ticket'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/tickets`.
 *
 * @returns One typed function per endpoint.
 */
export const useSupportApi = () => {
  const { request } = useApi()

  /**
   * Lists tickets, one page.
   *
   * @param filter - Page, page size, subject search, status, priority, department and flag.
   * @returns The page of ticket rows and the total count.
   */
  const listTickets = (filter: TicketFilter): Promise<PaginatedList<TicketListItem>> =>
    request<PaginatedList<TicketListItem>>(
      `/tickets${buildQuery({
        page: filter.page,
        pageSize: filter.pageSize,
        search: filter.search,
        status: filter.status,
        priority: filter.priority,
        departmentId: filter.departmentId,
        flagged: filter.flagged,
      })}`,
    )

  /**
   * Reads one ticket with its replies.
   *
   * @param id - Ticket id.
   * @returns The ticket.
   */
  const getTicket = (id: number | string): Promise<Ticket> => request<Ticket>(`${ApiPath.Tickets}/${id}`)

  /**
   * Reads the support overview figures.
   *
   * @param period - The reporting period.
   * @returns The figures for the period.
   */
  const getOverview = (period: SupportOverviewPeriod): Promise<SupportOverviewStats> =>
    request<SupportOverviewStats>(`${ApiPath.Tickets}/overview${buildQuery({ period })}`)

  /**
   * Lists the support departments.
   *
   * @returns Every department.
   */
  const listDepartments = (): Promise<Department[]> => request<Department[]>(ApiPath.TicketsDepartments)

  /**
   * Opens a ticket on behalf of a client.
   *
   * @param payload - Client, subject, message, department and priority.
   * @returns The new ticket's id.
   */
  const createTicket = (payload: CreateTicketPayload): Promise<number> =>
    request<number>(ApiPath.Tickets, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Changes a ticket's metadata.
   *
   * @param id - Ticket id.
   * @param payload - The members to change.
   * @returns Nothing.
   */
  const updateTicket = (id: number | string, payload: UpdateTicketPayload): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a ticket.
   *
   * @param id - Ticket id.
   * @returns Nothing.
   */
  const deleteTicket = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}`, { method: HttpMethod.Delete })

  /**
   * Adds a staff reply.
   *
   * @param id - Ticket id.
   * @param payload - Reply body.
   * @returns Nothing.
   */
  const replyToTicket = (id: number | string, payload: TicketReplyPayload): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}/reply`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Flips a ticket's flagged state.
   *
   * @param id - Ticket id.
   * @returns Nothing.
   */
  const toggleFlag = (id: number | string): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}/toggle-flag`, { method: HttpMethod.Post })

  /**
   * Runs one action on several tickets.
   *
   * @param payload - The ticket ids and the action.
   * @returns Nothing.
   */
  const bulkAction = (payload: TicketBulkPayload): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/bulk`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  return {
    listTickets, getTicket, getOverview, listDepartments,
    createTicket, updateTicket, deleteTicket, replyToTicket, toggleFlag, bulkAction,
  }
}
