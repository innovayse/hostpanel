/**
 * Pinia store for the fraud log.
 *
 * It is the only caller of the log endpoint of `useFraudApi`; a refusal becomes the API sentence
 * in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useFraudApi } from '@/composables/apis/useFraudApi'
import { runAction } from '@/lib/runAction'
import type { FraudLogEntry, FraudLogFilters } from '@/types/fraud'

/** Lists the fraud log one filtered page at a time. */
export const useFraudLogStore = defineStore('fraud-log', () => {
  const api = useFraudApi()

  /** Entries of the current page. */
  const entries = ref<FraudLogEntry[]>([])

  /** Total number of entries matching the filters, across all pages. */
  const totalCount = ref(0)

  /** True while a page is being read. */
  const loading = ref(false)

  /** Sentence for the last failed read, null when none. */
  const error = ref<string | null>(null)

  /**
   * Reads one filtered page of the log.
   *
   * @param filters - Decision and reason.
   * @param page - Page number (1-based).
   * @param pageSize - Entries per page.
   * @returns Promise that resolves when the page is read; a refusal is left in `error`.
   */
  const fetchPage = async (filters: FraudLogFilters, page: number, pageSize: number): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.listLog(filters, page, pageSize))
    loading.value = false
    if (result.ok) {
      entries.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  return { entries, totalCount, loading, error, fetchPage }
})
