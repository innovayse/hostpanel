/**
 * Pinia store for the admin VAT rates page.
 *
 * Holds the paged rate list and the create, update and delete of one row. The only caller of
 * `useVatRatesApi`; a refusal is kept as the API's own sentence, never thrown. A write does not
 * re-read the list: the screen does, because only it knows the page and the search.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useVatRatesApi } from '@/composables/apis/useVatRatesApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { SaveVatRatePayload, VatRateDto } from '@/types/vatrate'

/** Manages the VAT rate list and the admin VAT rate operations. */
export const useVatRatesStore = defineStore('vatRates', () => {
  const api = useVatRatesApi()

  /** The VAT rates of the loaded page, as the API lists them. */
  const vatRates = ref<VatRateDto[]>([])

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching rows across all pages. */
  const totalCount = ref(0)

  /** Whether a fetch is in progress. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed mutation (create, update, delete), null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Loads one page of VAT rates. The page and the search come from the list's address, so the store keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param search - The search text; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, search = ''): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listVatRates({ page, pageSize: pageSize.value, search: search.trim() }),
    )
    loading.value = false
    if (result.ok) {
      vatRates.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Adds a VAT rate.
   *
   * @param payload - The row's country, kind, rate and first day.
   * @returns True when created; false when refused (see `actionError`).
   */
  const create = async (payload: SaveVatRatePayload): Promise<boolean> => {
    return (await runAction(actionError, () => api.createVatRate(payload))).ok
  }

  /**
   * Replaces a VAT rate row's fields.
   *
   * @param id - The row's id.
   * @param payload - The row's new fields.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const update = async (id: number, payload: SaveVatRatePayload): Promise<boolean> => {
    return (await runAction(actionError, () => api.updateVatRate(id, payload))).ok
  }

  /**
   * Deletes a VAT rate row.
   *
   * @param id - The row's id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number): Promise<boolean> => {
    return (await runAction(actionError, () => api.deleteVatRate(id))).ok
  }

  return { vatRates, pageSize, totalCount, loading, error, actionError, fetchAll, create, update, remove }
})
