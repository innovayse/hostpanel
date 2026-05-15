import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { DashboardStats } from '../../../types/models'

/**
 * Pinia store for admin dashboard statistics.
 *
 * Fetches and caches stats from GET /api/admin/dashboard/stats.
 */
export const useDashboardStore = defineStore('dashboard', () => {
  const { request } = useApi()

  /** Dashboard statistics, null until loaded. */
  const stats = ref<DashboardStats | null>(null)

  /** True while stats are being fetched. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Loads dashboard statistics from the backend.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchStats(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      stats.value = await request<DashboardStats>('/admin/dashboard/stats')
    } catch {
      error.value = 'Failed to load dashboard stats.'
    } finally {
      loading.value = false
    }
  }

  return { stats, loading, error, fetchStats }
})
