import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { NetworkIssue, PagedResult } from '../../../types/models'

/**
 * Pinia store for managing network issues in the admin support module.
 *
 * Provides paginated listing with status filter, CRUD operations.
 */
export const useNetworkIssuesStore = defineStore('networkIssues', () => {
  const { request } = useApi()

  /** Paginated list of network issues. */
  const issues = ref<NetworkIssue[]>([])

  /** Total issue count. */
  const totalCount = ref(0)

  /** Currently loaded issue for editing. */
  const current = ref<NetworkIssue | null>(null)

  /** True while any API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /** Active status filter. */
  const statusFilter = ref<string>('Open')

  /** Current page (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /**
   * Fetches a paginated, filtered list of network issues.
   *
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value) })
      if (statusFilter.value) params.set('status', statusFilter.value)
      const result = await request<PagedResult<NetworkIssue>>(`/network-issues?${params}`)
      issues.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load network issues.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single network issue by ID.
   *
   * @param id - The issue identifier.
   */
  async function fetchById(id: number | string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<NetworkIssue>(`/network-issues/${id}`)
    } catch {
      error.value = 'Failed to load network issue.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Creates a new network issue.
   *
   * @param payload - Issue data.
   * @returns The new issue ID, or null on failure.
   */
  async function create(payload: {
    title: string
    type: string
    server: string | null
    priority: string
    status: string
    startDate: string
    endDate: string | null
    description: string
  }): Promise<number | null> {
    loading.value = true
    error.value = null
    try {
      return await request<number>('/network-issues', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    } catch {
      error.value = 'Failed to create network issue.'
      return null
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates an existing network issue.
   *
   * @param id - The issue identifier.
   * @param payload - Updated data.
   * @returns True on success.
   */
  async function update(id: number | string, payload: {
    title: string
    type: string
    server: string | null
    priority: string
    status: string
    startDate: string
    endDate: string | null
    description: string
  }): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      await request(`/network-issues/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      return true
    } catch {
      error.value = 'Failed to update network issue.'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * Deletes a network issue.
   *
   * @param id - The issue identifier.
   * @returns True on success.
   */
  async function remove(id: number | string): Promise<boolean> {
    error.value = null
    try {
      await request(`/network-issues/${id}`, { method: 'DELETE' })
      return true
    } catch {
      error.value = 'Failed to delete network issue.'
      return false
    }
  }

  return { issues, totalCount, current, loading, error, statusFilter, page, pageSize, fetchAll, fetchById, create, update, remove }
})
