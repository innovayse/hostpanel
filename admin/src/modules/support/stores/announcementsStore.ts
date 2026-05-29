import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { Announcement, PagedResult } from '../../../types/models'

/**
 * Pinia store for managing announcements in the admin support module.
 *
 * Provides paginated listing, CRUD operations for announcements.
 */
export const useAnnouncementsStore = defineStore('announcements', () => {
  const { request } = useApi()

  /** Paginated list of announcements. */
  const announcements = ref<Announcement[]>([])

  /** Total announcement count. */
  const totalCount = ref(0)

  /** Currently loaded announcement for editing. */
  const current = ref<Announcement | null>(null)

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /**
   * Fetches a paginated list of announcements.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await request<PagedResult<Announcement>>(
        `/announcements?page=${page.value}&pageSize=${pageSize.value}`,
      )
      announcements.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load announcements.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single announcement by ID.
   *
   * @param id - The announcement identifier.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<Announcement>(`/announcements/${id}`)
    } catch {
      error.value = 'Failed to load announcement.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new announcement.
   *
   * @param payload - Announcement data.
   * @returns The new announcement ID, or null on failure.
   */
  async function create(payload: { title: string; content: string; isPublished: boolean }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      return await request<number>('/announcements', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to create announcement.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates an existing announcement.
   *
   * @param id - The announcement identifier.
   * @param payload - Updated data.
   * @returns True on success.
   */
  async function update(id: number | string, payload: { title: string; content: string; isPublished: boolean }): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await request(`/announcements/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      return true
    } catch {
      error.value = 'Failed to update announcement.'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes an announcement.
   *
   * @param id - The announcement identifier.
   * @returns True on success.
   */
  async function remove(id: number | string): Promise<boolean> {
    error.value = null
    try {
      await request(`/announcements/${id}`, { method: 'DELETE' })
      return true
    } catch {
      error.value = 'Failed to delete announcement.'
      return false
    }
  }

  return { announcements, totalCount, current, loading, error, page, pageSize, fetchAll, fetchById, create, update, remove }
})
