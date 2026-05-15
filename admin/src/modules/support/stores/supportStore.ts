import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Ticket, PagedResult } from '../../../types/models'

/**
 * Pinia store for admin support tickets management.
 */
export const useSupportStore = defineStore('support', () => {
  const { request } = useApi()

  /** Loaded tickets list. */
  const tickets = ref<Ticket[]>([])

  /** Currently viewed ticket detail, null until loaded. */
  const current = ref<Ticket | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches all support tickets from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<Ticket>>('/tickets')
      tickets.value = result.items
    } catch {
      error.value = 'Failed to load tickets.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single ticket by ID.
   *
   * @param id - The ticket identifier.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<Ticket>(`/tickets/${id}`)
    } catch {
      error.value = 'Failed to load ticket details.'
    } finally {
      loading.value = false
    }
  }

  return { tickets, current, loading, error, fetchAll, fetchById }
})
