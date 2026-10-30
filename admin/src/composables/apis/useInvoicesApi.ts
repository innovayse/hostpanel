/**
 * One function per endpoint on the API invoice surface (`/api/billing`). No state lives here,
 * no error handling: a refusal rejects with an `ApiError` and the invoices store turns it
 * into a sentence.
 *
 * Callers are the invoices store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { DownloadedFile } from '@/types/download'
import type {
  CreatedId,
  CreateInvoicePayload,
  Invoice,
  InvoiceDetail,
  InvoiceFilters,
  InvoiceItemUpdate,
  InvoiceOptionsPayload,
  InvoicePaymentPayload,
  InvoiceRefundPayload,
} from '@/types/invoice'
import type { PaginatedList } from '@/types/paginatedlist'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for `/api/billing` invoices.
 *
 * @returns One typed function per invoice endpoint the admin panel uses.
 */
export const useInvoicesApi = () => {
  const { request, download } = useApi()

  /**
   * Lists invoices across all clients.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - Optional status and date filters.
   * @returns One page of invoices.
   */
  const list = (page: number, pageSize: number, filters: InvoiceFilters = {}): Promise<PaginatedList<Invoice>> =>
    request<PaginatedList<Invoice>>(`${ApiPath.Billing}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Lists the invoices of one client.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - Optional status and date filters.
   * @returns One page of invoices.
   */
  const listByClient = (
    clientId: number | string,
    page: number,
    pageSize: number,
    filters: InvoiceFilters = {},
  ): Promise<PaginatedList<Invoice>> =>
    request<PaginatedList<Invoice>>(`${ApiPath.Billing}/client/${clientId}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Loads one invoice with its lines and transactions.
   *
   * @param id - The invoice id.
   * @returns The invoice.
   */
  const get = (id: number): Promise<InvoiceDetail> => request<InvoiceDetail>(`${ApiPath.Billing}/${id}`)

  /**
   * Creates an invoice for a client.
   *
   * @param payload - Client, due date, lines and draft flag.
   * @returns The new invoice id.
   */
  const create = (payload: CreateInvoicePayload): Promise<CreatedId> =>
    request<CreatedId>(ApiPath.Billing, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces the line items of an invoice.
   *
   * @param id - The invoice id.
   * @param items - The item changes to apply.
   * @returns Nothing.
   */
  const updateItems = (id: number, items: InvoiceItemUpdate[]): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/items`, { method: HttpMethod.Put, body: JSON.stringify({ items }) })

  /**
   * Updates dates, payment method and tax rate.
   *
   * @param id - The invoice id.
   * @param options - The new options.
   * @returns Nothing.
   */
  const updateOptions = (id: number, options: InvoiceOptionsPayload): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/options`, { method: HttpMethod.Put, body: JSON.stringify(options) })

  /**
   * Updates the admin notes.
   *
   * @param id - The invoice id.
   * @param notes - Notes text.
   * @returns Nothing.
   */
  const updateNotes = (id: number, notes: string): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/notes`, { method: HttpMethod.Put, body: JSON.stringify({ notes }) })

  /**
   * Publishes a draft invoice (Draft to Unpaid).
   *
   * @param id - The invoice id.
   * @returns Nothing.
   */
  const publish = (id: number): Promise<void> => request<void>(`${ApiPath.Billing}/${id}/publish`, { method: HttpMethod.Post })

  /**
   * Cancels an invoice.
   *
   * @param id - The invoice id.
   * @returns Nothing.
   */
  const cancel = (id: number): Promise<void> => request<void>(`${ApiPath.Billing}/${id}/cancel`, { method: HttpMethod.Post })

  /**
   * Records a payment against an invoice.
   *
   * @param id - The invoice id.
   * @param payment - Payment details.
   * @returns Nothing.
   */
  const addPayment = (id: number, payment: InvoicePaymentPayload): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/payment`, { method: HttpMethod.Post, body: JSON.stringify(payment) })

  /**
   * Applies client credit to an invoice.
   *
   * @param id - The invoice id.
   * @param amount - Credit amount to apply.
   * @returns Nothing.
   */
  const applyCredit = (id: number, amount: number): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/credit`, { method: HttpMethod.Post, body: JSON.stringify({ amount }) })

  /**
   * Removes applied credit from an invoice.
   *
   * @param id - The invoice id.
   * @param amount - Credit amount to remove.
   * @returns Nothing.
   */
  const removeCredit = (id: number, amount: number): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/credit/remove`, { method: HttpMethod.Post, body: JSON.stringify({ amount }) })

  /**
   * Refunds a payment transaction.
   *
   * @param id - The invoice id.
   * @param payload - Refund details.
   * @returns Nothing.
   */
  const refund = (id: number, payload: InvoiceRefundPayload): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${id}/refund`, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Duplicates an invoice.
   *
   * @param id - The invoice id to duplicate.
   * @returns The new invoice id.
   */
  const duplicate = (id: number): Promise<CreatedId> =>
    request<CreatedId>(`${ApiPath.Billing}/${id}/duplicate`, { method: HttpMethod.Post })

  /**
   * Deletes an invoice.
   *
   * @param id - The invoice id.
   * @returns Nothing.
   */
  const remove = (id: number): Promise<void> => request<void>(`${ApiPath.Billing}/${id}`, { method: HttpMethod.Delete })

  /**
   * Applies one action to several invoices.
   *
   * @param invoiceIds - The invoice ids.
   * @param action - Action name, e.g. `MarkPaid`, `Duplicate`, `Delete`.
   * @returns Nothing.
   */
  const bulk = (invoiceIds: number[], action: string): Promise<void> =>
    request<void>(`${ApiPath.Billing}/bulk`, { method: HttpMethod.Post, body: JSON.stringify({ invoiceIds, action }) })

  /**
   * Downloads an invoice's PDF as a file the page can save.
   *
   * @param id - Invoice id.
   * @returns The PDF and the file name the API gave it.
   */
  const pdf = (id: number): Promise<DownloadedFile> => download(`${ApiPath.Billing}/${id}/pdf`)

  return {
    list,
    listByClient,
    get,
    pdf,
    create,
    updateItems,
    updateOptions,
    updateNotes,
    publish,
    cancel,
    addPayment,
    applyCredit,
    removeCredit,
    refund,
    duplicate,
    remove,
    bulk,
  }
}
