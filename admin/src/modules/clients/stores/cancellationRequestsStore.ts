import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { CancellationRequestItem, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin cancellation requests management.
 *
 * Cancellation requests are lazy-loaded — call {@link fetchAll} before reading {@link cancellations}.
 */
export const useCancellationRequestsStore = defineStore('cancellationRequests', () => {
  const { request } = useApi()

  /** Loaded cancellation requests list. */
  const cancellations = ref<CancellationRequestItem[]>([])

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Total number of matching items. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated cancellation requests from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<CancellationRequestItem>>(
        `/admin/cancellation-requests?page=${page.value}&pageSize=${pageSize.value}`,
      )
      cancellations.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load cancellation requests.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes a cancellation request by ID, then re-fetches the current page.
   *
   * @param id - The cancellation request ID to delete.
   * @returns Promise that resolves when the deletion and re-fetch complete.
   */
  async function deleteRequest(id: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      await request<void>(`/admin/cancellation-requests/${id}`, { method: 'DELETE' })
      await fetchAll()
    } catch {
      error.value = 'Failed to delete cancellation request.'
      loading.value = false
    }
  }

  return { cancellations, page, pageSize, totalCount, loading, error, fetchAll, deleteRequest }
})
