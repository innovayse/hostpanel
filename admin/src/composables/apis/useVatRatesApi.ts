/**
 * Endpoints behind the VAT rates store: the paged rate list and the create, update and delete of one row.
 *
 * No state lives here; every function rejects with an `ApiError` carrying the problem+json body.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { SaveVatRatePayload, VatRateDto, VatRateFilter } from '@/types/vatrate'

/**
 * Endpoint functions for `/admin/vat-rates`.
 *
 * @returns One typed function per endpoint.
 */
export const useVatRatesApi = () => {
  /** The shared request function every endpoint below sends through. */
  const { request } = useApi()

  /**
   * Lists VAT rates, one page.
   *
   * @param filter - Page, page size and search.
   * @returns The page and the total count.
   */
  const listVatRates = (filter: VatRateFilter): Promise<PaginatedList<VatRateDto>> =>
    request<PaginatedList<VatRateDto>>(
      `${ApiPath.AdminVatRates}${buildQuery({ page: filter.page, pageSize: filter.pageSize, search: filter.search })}`,
    )

  /**
   * Adds the VAT rate of a member state from a given day on.
   *
   * @param payload - The row's country, kind, rate and first day.
   * @returns The new row's id.
   */
  const createVatRate = (payload: SaveVatRatePayload): Promise<number> =>
    request<number>(ApiPath.AdminVatRates, { method: HttpMethod.Post, body: JSON.stringify(payload) })

  /**
   * Replaces a VAT rate row's fields.
   *
   * @param id - The row's id.
   * @param payload - The row's new fields.
   * @returns Nothing; the API answers 204.
   */
  const updateVatRate = (id: number, payload: SaveVatRatePayload): Promise<void> =>
    request<void>(`${ApiPath.AdminVatRates}/${id}`, { method: HttpMethod.Put, body: JSON.stringify(payload) })

  /**
   * Deletes a VAT rate row; invoices already issued keep the rate they were taxed at.
   *
   * @param id - The row's id.
   * @returns Nothing; the API answers 204.
   */
  const deleteVatRate = (id: number): Promise<void> =>
    request<void>(`${ApiPath.AdminVatRates}/${id}`, { method: HttpMethod.Delete })

  return { listVatRates, createVatRate, updateVatRate, deleteVatRate }
}
