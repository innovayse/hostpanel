/**
 * One function per ticket endpoint the client profile uses (`/api/tickets`). No state lives
 * here, no error handling.
 *
 * Callers are the client ticket store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { ClientTicketStats } from '@/types/client'
import type { Department } from '@/types/department'
import type { PaginatedList } from '@/types/paginatedlist'
import type { ClientTicketFilter, CreateTicketPayload, Ticket, TicketListItem, UpdateTicketPayload } from '@/types/ticket'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for the tickets of a client.
 *
 * @returns One typed function per ticket endpoint the client profile uses.
 */
export const useClientTicketsApi = () => {
  const { request } = useApi()

  /**
   * Lists the tickets of a client.
   *
   * @param clientId - The client id.
   * @param filter - Page, page size, subject search, status, priority and department.
   * @returns One page of tickets.
   */
  const list = (clientId: number | string, filter: ClientTicketFilter): Promise<PaginatedList<TicketListItem>> =>
    request<PaginatedList<TicketListItem>>(
      `${ApiPath.TicketsClient}/${clientId}${buildQuery({
        page: filter.page,
        pageSize: filter.pageSize,
        search: filter.search,
        status: filter.status,
        priority: filter.priority,
        departmentId: filter.departmentId,
      })}`,
    )

  /**
   * Loads the ticket statistics of a client.
   *
   * @param clientId - The client id.
   * @returns The summary-card figures.
   */
  const stats = (clientId: number | string): Promise<ClientTicketStats> =>
    request<ClientTicketStats>(`${ApiPath.TicketsClient}/${clientId}/stats`)

  /**
   * Loads one ticket with its replies.
   *
   * @param id - The ticket id.
   * @returns The ticket.
   */
  const get = (id: number | string): Promise<Ticket> => request<Ticket>(`${ApiPath.Tickets}/${id}`)

  /**
   * Loads the support departments.
   *
   * @returns The departments.
   */
  const departments = (): Promise<Department[]> => request<Department[]>(ApiPath.TicketsDepartments)

  /**
   * Opens a ticket on behalf of a client.
   *
   * @param payload - Ticket fields.
   * @returns The new ticket id.
   */
  const create = (payload: CreateTicketPayload): Promise<number> =>
    request<number>(ApiPath.Tickets, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates ticket metadata.
   *
   * @param id - The ticket id.
   * @param payload - Fields to change.
   * @returns Nothing.
   */
  const update = (id: number | string, payload: UpdateTicketPayload): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a ticket.
   *
   * @param id - The ticket id.
   * @returns Nothing.
   */
  const remove = (id: number | string): Promise<void> => request<void>(`${ApiPath.Tickets}/${id}`, { method: HttpMethod.Delete })

  /**
   * Adds a staff reply to a ticket.
   *
   * @param id - The ticket id.
   * @param message - Reply body.
   * @returns Nothing.
   */
  const reply = (id: number | string, message: string): Promise<void> =>
    request<void>(`${ApiPath.Tickets}/${id}/reply`, { method: HttpMethod.Post, body: JSON.stringify({ message }) })

  return { list, stats, get, departments, create, update, remove, reply }
}
