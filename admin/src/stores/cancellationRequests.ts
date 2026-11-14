/**
 * Pinia store for admin cancellation requests.
 *
 * Lazy-loaded: call `fetchAll` before reading `cancellations`. It is the only caller of
 * `useCancellationRequestsApi`; a refusal becomes the API sentence in `error`. The page and the filters are the
 * address of the list, so the store keeps no copy of them.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useCancellationRequestsApi } from '@/composables/apis/useCancellationRequestsApi'
import { runAction } from '@/lib/runAction'
import type { CancellationRequestFilters, CancellationRequestItem } from '@/types/service'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Lists and deletes service cancellation requests. */
export const useCancellationRequestsStore = defineStore('cancellationRequests', () => {
  const api = useCancellationRequestsApi()

  /** Loaded cancellation requests list. */
  const cancellations = ref<CancellationRequestItem[]>([])

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching items. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /**
   * Fetches one page of cancellation requests. The page, the search text and the filters come from the address of
   * the list, so the store keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text, type and status filters; empty for none.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: CancellationRequestFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.list(page, pageSize.value, { ...filters, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      cancellations.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Deletes a cancellation request; the list screen reloads the page it shows.
   *
   * @param id - The request id to delete.
   * @returns True when deleted; false when refused (see `error`).
   */
  const deleteRequest = async (id: number): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.remove(id))
    loading.value = false
    return result.ok
  }

  return { cancellations, pageSize, totalCount, loading, error, fetchAll, deleteRequest }
})
