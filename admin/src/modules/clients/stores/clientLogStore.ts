import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { ActivityLog } from '../../../types/models'

/**
 * Manages activity log state for the client Log page.
 *
 * Logs are lazy-loaded — call fetchLogs before reading logs.
 */
export const useClientLogStore = defineStore('clientLog', () => {
  const { request } = useApi()

  /** Fetched activity log entries. */
  const logs = ref<ActivityLog[]>([])

  /** Total number of matching entries. */
  const totalCount = ref(0)

  /** True while an API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated, filtered activity logs for a client.
   *
   * @param clientId - The client's primary key.
   * @param page - 1-based page number.
   * @param pageSize - Entries per page.
   * @param filters - Optional filter parameters.
   * @returns Promise that resolves when logs are loaded.
   */
  async function fetchLogs(
    clientId: number,
    page: number,
    pageSize: number,
    filters?: { date?: string; adminSearch?: string; description?: string; ipAddress?: string },
  ): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
      })
      if (filters?.date) params.set('date', filters.date)
      if (filters?.adminSearch) params.set('adminSearch', filters.adminSearch)
      if (filters?.description) params.set('description', filters.description)
      if (filters?.ipAddress) params.set('ipAddress', filters.ipAddress)

      const data = await request<{ items: ActivityLog[]; totalCount: number }>(
        `/clients/${clientId}/activity-logs?${params.toString()}`,
      )
      logs.value = data.items
      totalCount.value = data.totalCount
    } catch {
      error.value = 'Failed to load activity log'
    } finally {
      loading.value = false
    }
  }

  /**
   * Resets the store to its initial state.
   *
   * @returns void
   */
  function reset(): void {
    logs.value = []
    totalCount.value = 0
    loading.value = false
    error.value = null
  }

  return { logs, totalCount, loading, error, fetchLogs, reset }
})
