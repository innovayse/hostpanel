/**
 * One function per endpoint on the API quote surface (`/api/billing/quotes`, `/api/quotes`).
 * No state lives here, no error handling: a refusal rejects with an `ApiError` and the quote
 * store turns it into a sentence.
 *
 * Callers are the quote store and the client summary store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type { CreateQuotePayload, Quote, QuoteFilters, QuoteListItem, UpdateQuotePayload } from '@/types/quote'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'

/**
 * Endpoint functions for quotes.
 *
 * @returns One typed function per quote endpoint the admin panel uses.
 */
export const useQuotesApi = () => {
  const { request } = useApi()

  /**
   * Lists quotes across all clients.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text and the client, stage and validity filters; a member left out is not sent.
   * @returns One page of quotes.
   */
  const list = (page: number, pageSize: number, filters: QuoteFilters = {}): Promise<PaginatedList<QuoteListItem>> =>
    request<PaginatedList<QuoteListItem>>(`${ApiPath.BillingQuotes}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Lists the quotes of one client.
   *
   * @param clientId - The client id.
   * @param page - Page number (1-based).
   * @param pageSize - Items per page.
   * @param filters - The search text and the stage and validity filters; a member left out is not sent.
   * @returns One page of quotes.
   */
  const listByClient = (
    clientId: number | string,
    page: number,
    pageSize: number,
    filters: QuoteFilters = {},
  ): Promise<PaginatedList<QuoteListItem>> =>
    request<PaginatedList<QuoteListItem>>(`${ApiPath.BillingQuotes}/client/${clientId}${buildQuery({ page, pageSize, ...filters })}`)

  /**
   * Loads one quote with its lines.
   *
   * @param id - The quote id.
   * @returns The quote.
   */
  const get = (id: number): Promise<Quote> => request<Quote>(`${ApiPath.Quotes}/${id}`)

  /**
   * Creates a quote.
   *
   * @param payload - Quote fields and lines.
   * @returns The new quote id.
   */
  const create = (payload: CreateQuotePayload): Promise<number> =>
    request<number>(ApiPath.BillingQuotes, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Updates a quote.
   *
   * @param id - The quote id.
   * @param payload - Quote fields and lines.
   * @returns Nothing.
   */
  const update = (id: number, payload: UpdateQuotePayload): Promise<void> =>
    request<void>(`${ApiPath.Quotes}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Duplicates a quote.
   *
   * @param id - The quote id.
   * @returns The new quote id.
   */
  const duplicate = (id: number): Promise<number> => request<number>(`${ApiPath.Quotes}/${id}/duplicate`, { method: HttpMethod.Post })

  /**
   * Converts a quote to an invoice.
   *
   * @param id - The quote id.
   * @returns The new invoice id.
   */
  const convertToInvoice = (id: number): Promise<number> =>
    request<number>(`${ApiPath.Quotes}/${id}/convert-to-invoice`, { method: HttpMethod.Post })

  /**
   * Deletes a quote.
   *
   * @param id - The quote id.
   * @returns Nothing.
   */
  const remove = (id: number): Promise<void> => request<void>(`${ApiPath.Quotes}/${id}`, { method: HttpMethod.Delete })

  return { list, listByClient, get, create, update, duplicate, convertToInvoice, remove }
}
