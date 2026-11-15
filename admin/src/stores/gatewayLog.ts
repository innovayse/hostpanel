/**
 * Pinia store for the payment-gateway log.
 *
 * It is the only caller of `useGatewayLogApi`; a refusal becomes the API sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useGatewayLogApi } from '@/composables/apis/useGatewayLogApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { GatewayLogEntry, GatewayLogFilters } from '@/types/gatewaylog'

/** Lists the gateway log one filtered page at a time. */
export const useGatewayLogStore = defineStore('gateway-log', () => {
  const api = useGatewayLogApi()

  /** Entries of the current page. */
  const entries = ref<GatewayLogEntry[]>([])

  /** Total number of entries matching the filters, across all pages. */
  const totalCount = ref(0)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /**
   * Loads one page of the log, narrowed by the server. The page, the search, the period and the result come from the
   * list's address, so the store keeps no copy of them.
   *
   * @param page - Page number (1-based).
   * @param filters - The search text, the period and the result; a member left out does not narrow.
   * @returns Promise that resolves when the page is loaded.
   */
  const fetchPage = async (page: number, filters: GatewayLogFilters = {}): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.list({ ...filters, search: filters.search?.trim() }, page, pageSize.value),
    )
    loading.value = false
    if (result.ok) {
      entries.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  return { entries, totalCount, pageSize, loading, error, fetchPage }
})
