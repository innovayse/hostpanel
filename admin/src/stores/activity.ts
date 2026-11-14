/**
 * Admin notification feed store: recent events for the header bell.
 */

import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { useDashboardApi } from '@/composables/apis/useDashboardApi'
import { runAction } from '@/lib/runAction'
import type { ActivityEvent } from '@/types/activityevent'

/**
 * Pinia store for the admin notification feed.
 *
 * Fetches recent events (new clients, overdue invoices, expiring domains) through
 * `useDashboardApi`; a refusal shows the API's own sentence.
 */
export const useActivityStore = defineStore('activity', () => {
  const api = useDashboardApi()

  /** Recent events, newest first. Empty until loaded. */
  const events = ref<ActivityEvent[]>([])

  /** True while events are being fetched. */
  const loading = ref(false)

  /** True until the first read has answered: nothing is held yet, so a list draws placeholders instead of an empty state. */
  const initialLoading = computed<boolean>(() => loading.value && events.value.length === 0)

  /** Sentence for the last failed call, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Loads the recent activity feed from the backend.
   *
   * @param limit - Maximum number of events to fetch.
   * @returns Promise that resolves when the attempt is over (see `error`).
   */
  const fetchActivity = async (limit = 10): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.fetchActivity(limit))
    if (result.ok) {
      events.value = result.value
    }
    loading.value = false
  }

  return { events, loading, initialLoading, error, fetchActivity }
})
