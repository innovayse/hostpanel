/**
 * Pinia store for the activity log of one client profile.
 *
 * Lazy-loaded: call `fetchLogs` before reading `logs`. It reads through
 * `useClientHistoryApi`; a refusal becomes the API sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientHistoryApi } from '@/composables/apis/useClientHistoryApi'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { ActivityLog, ActivityLogFilters } from '@/types/activitylog'

/** Loads the activity log of a client. */
export const useClientLogStore = defineStore('clientLog', () => {
  const api = useClientHistoryApi()

  /** Fetched activity log entries. */
  const logs = ref<ActivityLog[]>([])

  /** Total number of matching entries. */
  const totalCount = ref(0)

  /** Items per page. */
  const pageSize = ref(LEDGER_PAGE_SIZE)

  /** True while an API request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /**
   * Fetches one page of the activity log of a client, narrowed by the server. The page, the search and the period come
   * from the list's address, so the store keeps no copy of them.
   *
   * @param clientId - The client primary key.
   * @param page - 1-based page number.
   * @param filters - The search text and the period; a member left out does not narrow.
   * @returns Promise that resolves when logs are loaded.
   */
  const fetchLogs = async (clientId: number, page: number, filters: ActivityLogFilters = {}): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.activityLogs(clientId, page, pageSize.value, { ...filters, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      logs.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Resets the store to its initial state.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    logs.value = []
    totalCount.value = 0
    loading.value = false
    error.value = null
  }

  return { logs, totalCount, pageSize, loading, error, fetchLogs, reset }
})
