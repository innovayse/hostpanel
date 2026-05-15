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

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all invoices from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<Invoice>>('/billing')
      invoices.value = result.items
    } catch {
      error.value = 'Failed to load invoices.'
    } finally {
      loading.value = false
    }
  }

  return { invoices, loading, error, fetchAll }
})
