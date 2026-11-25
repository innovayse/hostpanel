/**
 * Admin notification feed store: recent events for the header bell and which of them the admin has read.
 */

import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { useDashboardApi } from '@/composables/apis/useDashboardApi'
import { runAction } from '@/lib/runAction'
import type { ActivityEvent } from '@/types/activityevent'

/** How many events the bell previews. */
const DEFAULT_LIMIT = 10

/**
 * Pinia store for the admin notification feed.
 *
 * Fetches recent events (new clients, overdue invoices, payments, opened tickets, expiring domains)
 * through `useDashboardApi`; a refusal shows the API's own sentence. Each event arrives with `isRead`
 * for the signed-in admin, and marking one or all read goes through the API, so the state follows the
 * admin to any browser.
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

  /** How many events the last read asked for, so a re-read after a write asks for the same window. */
  const lastLimit = ref(DEFAULT_LIMIT)

  /** The events not read yet, newest first. */
  const unreadEvents = computed<ActivityEvent[]>(() => events.value.filter((event) => !event.isRead))

  /** How many events are not read yet. */
  const unreadCount = computed<number>(() => unreadEvents.value.length)

  /**
   * Loads the recent activity feed from the backend.
   *
   * @param limit - Maximum number of events to fetch.
   * @returns Promise that resolves when the attempt is over (see `error`).
   */
  const fetchActivity = async (limit = DEFAULT_LIMIT): Promise<void> => {
    lastLimit.value = limit
    loading.value = true
    const result = await runAction(error, () => api.fetchActivity(limit))
    if (result.ok) {
      events.value = result.value
    }
    loading.value = false
  }

  /**
   * Marks one event as read on the server, then reads the feed again.
   *
   * @param event - The event the operator opened or dismissed.
   * @returns Promise that resolves when the attempt is over (see `error`).
   */
  const markRead = async (event: ActivityEvent): Promise<void> => {
    const result = await runAction(error, () => api.markActivityRead(event.key))
    if (result.ok) {
      await fetchActivity(lastLimit.value)
    }
  }

  /**
   * Marks every event of the feed as read on the server, then reads the feed again.
   *
   * @returns Promise that resolves when the attempt is over (see `error`).
   */
  const markAllRead = async (): Promise<void> => {
    const result = await runAction(error, () => api.markAllActivityRead())
    if (result.ok) {
      await fetchActivity(lastLimit.value)
    }
  }

  return { events, loading, initialLoading, error, unreadEvents, unreadCount, fetchActivity, markRead, markAllRead }
})
