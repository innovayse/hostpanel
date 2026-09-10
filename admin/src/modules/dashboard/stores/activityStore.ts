import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { ActivityEvent } from '../../../types/activityevent'

/**
 * Pinia store for the admin notification feed.
 *
 * Fetches recent events (new clients, overdue invoices, expiring domains) from
 * GET /api/admin/dashboard/activity.
 */
export const useActivityStore = defineStore('activity', () => {
  const { request } = useApi()

  /** Recent events, newest first. Empty until loaded. */
  const events = ref<ActivityEvent[]>([])

  /** True while events are being fetched. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Loads the recent activity feed from the backend.
   *
   * @param limit - Maximum number of events to fetch.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchActivity(limit = 10): Promise<void> {
    loading.value = true
    error.value = null
    try {
      events.value = await request<ActivityEvent[]>(`/admin/dashboard/activity?limit=${limit}`)
    } catch {
      error.value = 'Failed to load notifications.'
    } finally {
      loading.value = false
    }
  }

  return { events, loading, error, fetchActivity }
})
