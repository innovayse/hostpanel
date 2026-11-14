/**
 * One function per endpoint on the API billable-item surface: the admin-wide list under
 * `/api/billing/billable-items` and the client-scoped one under
 * `/api/clients/{id}/billable-items`. No state lives here, no error handling.
 *
 * Callers are the billable-item stores, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type {
  BillableItem,
  BillableItemListFilters,
  BillableItemsResult,
  ClientBillableItemFilters,
  CreateBillableItemPayload,
  CreateClientBillableItemPayload,
} from '@/types/billableitem'
import type { PaginatedList } from '@/types/paginatedlist'
import type { ServiceListItem } from '@/types/service'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import { LOOKUP_PAGE_SIZE } from '@/lib/limits'

/**
 * Endpoint functions for billable items.
 *
 * @returns One typed function per billable-item endpoint the admin panel uses.
 */
export const useBillableItemsApi = () => {
  const { request } = useApi()

  /**
   * Lists billable items across all clients.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text, client, type and invoiced filters; empty for none.
   * @returns One page of items and the total count of matches.
   */
  const list = (page: number, pageSize: number, filters: BillableItemListFilters = {}): Promise<PaginatedList<BillableItem>> =>
    request<PaginatedList<BillableItem>>(`${ApiPath.BillingBillableItems}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Creates a billable item.
   *
   * @param payload - Item fields.
   * @returns Nothing.
   */
  const create = (payload: CreateBillableItemPayload): Promise<void> =>
    request<void>(ApiPath.BillingBillableItems, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Deletes a billable item.
   *
   * @param id - The item id.
   * @returns Nothing.
   */
  const remove = (id: number): Promise<void> => request<void>(`${ApiPath.BillingBillableItems}/${id}`, { method: HttpMethod.Delete })

  /**
   * Loads a client uninvoiced items and one page of invoiced items; the filters narrow both.
   *
   * @param clientId - The client id.
   * @param page - Invoiced page, 1-based.
   * @param pageSize - Invoiced items per page.
   * @param filters - The search text and type filter; empty for none.
   * @returns The client items.
   */
  const listByClient = (
    clientId: string,
    page: number,
    pageSize: number,
    filters: ClientBillableItemFilters = {},
  ): Promise<BillableItemsResult> =>
    request<BillableItemsResult>(
      `${ApiPath.Clients}/${clientId}/billable-items${buildQuery({ page, pageSize, ...filters })}`,
    )

  /**
   * Invoices the given uninvoiced items of a client.
   *
   * @param clientId - The client id.
   * @param billableItemIds - The item ids to invoice.
   * @returns Nothing.
   */
  const invoiceSelected = (clientId: string, billableItemIds: number[]): Promise<void> =>
    request<void>(`${ApiPath.Clients}/${clientId}/billable-items/invoice-selected`, {
      method: HttpMethod.Post,
      body: JSON.stringify({ billableItemIds }),
    })

  /**
   * Deletes one billable item of a client.
   *
   * @param clientId - The client id.
   * @param id - The item id.
   * @returns Nothing.
   */
  const removeForClient = (clientId: string, id: number): Promise<void> =>
    request<void>(`${ApiPath.Clients}/${clientId}/billable-items/${id}`, { method: HttpMethod.Delete })

  /**
   * Lists a client services for the item dropdowns.
   *
   * @param clientId - The client id.
   * @returns One page of services (up to 100).
   */
  const listServices = (clientId: string): Promise<PaginatedList<ServiceListItem>> =>
    request<PaginatedList<ServiceListItem>>(`${ApiPath.Services}${buildQuery({ clientId, page: 1, pageSize: LOOKUP_PAGE_SIZE })}`)

  /**
   * Adds one billable item to a client.
   *
   * @param clientId - The client id.
   * @param payload - The item, shaped as the API's create command (the route names the client).
   * @returns Nothing.
   */
  const addForClient = (clientId: string, payload: CreateClientBillableItemPayload): Promise<void> =>
    request<void>(`${ApiPath.Clients}/${clientId}/billable-items`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Adds time-billing entries to a client.
   *
   * @param clientId - The client id.
   * @param entries - The entries to bill.
   * @returns Nothing.
   */
  const addTimeBilling = (clientId: string, entries: Record<string, unknown>[]): Promise<void> =>
    request<void>(`${ApiPath.Clients}/${clientId}/billable-items/time-billing`, {
      method: HttpMethod.Post,
      body: JSON.stringify({ entries }),
    })

  return {
    list,
    create,
    remove,
    listByClient,
    invoiceSelected,
    removeForClient,
    listServices,
    addForClient,
    addTimeBilling,
  }
}
