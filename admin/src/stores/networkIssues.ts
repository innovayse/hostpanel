/**
 * Pinia store for network issues in the admin support module.
 *
 * Owns every request the network issue pages make and the sentence for a refusal, so the
 * pages hold no `catch`.
 */
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useNetworkIssuesApi } from '@/composables/apis/useNetworkIssuesApi'
import { runAction } from '@/lib/runAction'
import type { NetworkIssue, NetworkIssueListFilters, NetworkIssuePayload } from '@/types/networkissue'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Paginated, searchable and filterable listing and CRUD of network issues. */
export const useNetworkIssuesStore = defineStore('networkIssues', () => {
  const api = useNetworkIssuesApi()

  /** Paginated list of network issues. */
  const issues = ref<NetworkIssue[]>([])

  /** Total issue count. */
  const totalCount = ref(0)

  /** Currently loaded issue for editing. */
  const current = ref<NetworkIssue | null>(null)

  /** True while a load or save request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load or save, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last refused delete, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused save, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /**
   * Loads one page of network issues, narrowed by the server. The page, the search and the filters come from the list's
   * address, so the store keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text and the status, type and priority filters; a member left out does not narrow.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: NetworkIssueListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.listNetworkIssues({ ...filters, page, pageSize: pageSize.value, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      issues.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Loads one network issue into `current`.
   *
   * @param id - The issue identifier.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchById = async (id: number | string): Promise<boolean> => {
    loading.value = true
    current.value = null
    const result = await runAction(error, () => api.getNetworkIssue(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
    return result.ok
  }

  /**
   * Creates a network issue.
   *
   * @param payload - Issue data.
   * @returns The new issue's id, or null when refused (see `error` and `fieldErrors`).
   */
  const create = async (payload: NetworkIssuePayload): Promise<number | null> => {
    loading.value = true
    const result = await runAction(error, () => api.createNetworkIssue(payload), fieldErrors)
    loading.value = false
    return result.ok ? result.value : null
  }

  /**
   * Replaces a network issue.
   *
   * @param id - The issue identifier.
   * @param payload - Updated data.
   * @returns True when saved; false when refused (see `error` and `fieldErrors`).
   */
  const update = async (id: number | string, payload: NetworkIssuePayload): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.updateNetworkIssue(id, payload), fieldErrors)
    loading.value = false
    return result.ok
  }

  /**
   * Deletes a network issue.
   *
   * @param id - The issue identifier.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const remove = async (id: number | string): Promise<boolean> =>
    (await runAction(actionError, () => api.deleteNetworkIssue(id))).ok

  return {
    issues, totalCount, current, loading, error, actionError, fieldErrors, pageSize,
    fetchAll, fetchById, create, update, remove,
  }
})
