/**
 * Pinia store for the fraud log.
 *
 * It is the only caller of the log endpoint of `useFraudApi`; a refusal becomes the API sentence
 * in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useFraudApi } from '@/composables/apis/useFraudApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { FraudLogEntry, FraudLogFilters } from '@/types/fraud'

/** Lists the fraud log one filtered page at a time. */
export const useFraudLogStore = defineStore('fraud-log', () => {
  const api = useFraudApi()

  /** Entries of the current page. */
  const entries = ref<FraudLogEntry[]>([])

  /** Entries per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of entries matching the filters, across all pages. */
  const totalCount = ref(0)

  /** True while a page is being read. */
  const loading = ref(false)

  /** Sentence for the last failed read, null when none. */
  const error = ref<string | null>(null)

  /**
   * Reads one filtered page of the log.
   *
   * @param page - Page number (1-based).
   * @param filters - Search text, kind, decision and reason; a member left out means no narrowing.
   * @returns True when read; false when refused (see `error`).
   */
  const fetchPage = async (page: number, filters: FraudLogFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.listLog(filters, page, pageSize.value))
    loading.value = false
    if (result.ok) {
      entries.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  return { entries, pageSize, totalCount, loading, error, fetchPage }
})
