/**
 * Pinia store for the admin plan change requests: the filtered, paged list and the approve and
 * reject decisions.
 *
 * Lazy-loaded: call `fetchAll` before reading `requests`. It is the only caller of
 * `useServiceChangeRequestsApi`; a refusal becomes the API sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useServiceChangeRequestsApi } from '@/composables/apis/useServiceChangeRequestsApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { ServiceChangeRequestItem, ServiceChangeStatus } from '@/types/servicechangerequest'

/** Lists plan change requests and decides them. */
export const useServiceChangeRequestsStore = defineStore('serviceChangeRequests', () => {
  /** The plan change request endpoints; this store is their only caller. */
  const api = useServiceChangeRequestsApi()

  /** Loaded page of requests. */
  const requests = ref<ServiceChangeRequestItem[]>([])

  /** Status filter; empty for every status. */
  const status = ref<ServiceChangeStatus | ''>('')

  /** Service filter, the history of one service; null for every service. */
  const serviceId = ref<number | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Total number of matching requests. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused decision, null when none. */
  const actionError = ref<string | null>(null)

  /**
   * Fetches the current page with the current filters.
   *
   * @returns Promise that resolves when data is loaded; a refusal is left in `error`.
   */
  const fetchAll = async (): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.list(status.value, serviceId.value, page.value, pageSize.value))
    loading.value = false
    if (result.ok) {
      requests.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Sends one decision and reloads the current page when it succeeds.
   *
   * @param task - The decision request.
   * @returns True when decided; false when refused (see `actionError`).
   */
  const decide = async (task: () => Promise<void>): Promise<boolean> => {
    loading.value = true
    const result = await runAction(actionError, task)
    loading.value = false
    if (result.ok) {
      await fetchAll()
    }
    return result.ok
  }

  /**
   * Approves a request, then reloads the current page.
   *
   * @param id - The request id.
   * @param note - Optional staff note.
   * @returns True when approved; false when refused (see `actionError`).
   */
  const approve = (id: number, note: string | null): Promise<boolean> => decide(() => api.approve(id, note))

  /**
   * Rejects a request, then reloads the current page.
   *
   * @param id - The request id.
   * @param note - Optional staff note.
   * @returns True when rejected; false when refused (see `actionError`).
   */
  const reject = (id: number, note: string | null): Promise<boolean> => decide(() => api.reject(id, note))

  return { requests, status, serviceId, page, pageSize, totalCount, loading, error, actionError, fetchAll, approve, reject }
})
