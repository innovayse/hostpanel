import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Invoice, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin billing/invoices management.
 */
export const useBillingStore = defineStore('billing', () => {
  const { request } = useApi()

  /** Loaded invoices list. */
  const invoices = ref<Invoice[]>([])

  /** Total count of invoices. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /** Current page. */
  const page = ref(1)

  /** Page size. */
  const pageSize = ref(20)

  /**
   * Fetches all invoices from the backend.
   *
   * @param p Page number (1-based)
   * @param ps Page size
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(p = 1, ps = 20): Promise<void> {
    page.value = p
    pageSize.value = ps
    loading.value = true
    error.value = null
    try {
      const query = new URLSearchParams({ page: String(p), pageSize: String(ps) }).toString()
      const result = await request<PagedResult<Invoice>>(`/billing?${query}`)
      invoices.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load invoices.'
    } finally {
      loading.value = false
    }
  }

  return { invoices, totalCount, loading, error, page, pageSize, fetchAll }
})
