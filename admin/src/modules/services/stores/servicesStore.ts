import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { ServiceListItem, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin services management.
 *
 * Provides paginated service listing with enriched client, domain, and pricing data.
 */
export const useServicesStore = defineStore('services', () => {
  const { request } = useApi()

  /** Loaded services list for the current page. */
  const services = ref<ServiceListItem[]>([])

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Total number of matching items across all pages. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches paginated services from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<ServiceListItem>>(
        `/services?page=${page.value}&pageSize=${pageSize.value}`,
      )
      services.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load services.'
    } finally {
      loading.value = false
    }
  }

  return { services, page, pageSize, totalCount, loading, error, fetchAll }
})
