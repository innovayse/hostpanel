/**
 * Pinia store for admin user management (Identity users and the client accounts linked to
 * them).
 *
 * Handles the paged list, single-user detail, profile edits and password operations through
 * `useUsersApi`. A refusal becomes the API sentence in `error` (loads) or `actionError`
 * (mutations), so no view holds a `catch`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useUsersApi } from '@/composables/apis/useUsersApi'
import { runAction } from '@/lib/runAction'
import type { UpdateUserPayload, UserDetail, UserListFilters, UserListItem } from '@/types/useraccount'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'

/** Lists, opens and changes Identity users. */
export const useUsersStore = defineStore('clientAccounts', () => {
  const api = useUsersApi()

  /** Loaded users list. */
  const users = ref<UserListItem[]>([])

  /** Total users matching the current query. */
  const totalCount = ref(0)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** Currently viewed user detail. */
  const current = ref<UserDetail | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /** Sentence for the last failed update, delete, reset or password change, null when none. */
  const actionError = ref<string | null>(null)

  /** Per-field messages of the last refused mutation, keyed by camelCase field name. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Fetches one page of the user list. The page and the search text come from the list's address, so the store
   * keeps no copy of them.
   *
   * @param page - Page to load, 1-based.
   * @param filters - The search text; empty for every user.
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchAll = async (page: number, filters: UserListFilters = {}): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.list(page, pageSize.value, filters.search))
    loading.value = false
    if (result.ok) {
      users.value = result.value.items
      totalCount.value = result.value.totalCount
    }
    return result.ok
  }

  /**
   * Fetches a single user with linked accounts.
   *
   * @param id - Identity user id.
   * @returns Promise that resolves when data is loaded.
   */
  const fetchById = async (id: string): Promise<void> => {
    loading.value = true
    current.value = null
    const result = await runAction(error, () => api.get(id))
    loading.value = false
    if (result.ok) {
      current.value = result.value
    }
  }

  /**
   * Runs one mutating request, capturing a refusal into `actionError`.
   *
   * @param task - The request to run.
   * @returns True when the API accepted it; false when refused.
   */
  const mutate = async (task: () => Promise<void>): Promise<boolean> => {
    const result = await runAction(actionError, task, fieldErrors)
    return result.ok
  }

  /**
   * Updates a user profile.
   *
   * @param id - Identity user id.
   * @param data - Updated profile fields.
   * @returns True when saved; false when refused (see `actionError`).
   */
  const updateUser = (id: string, data: UpdateUserPayload): Promise<boolean> => mutate(() => api.update(id, data))

  /**
   * Deletes a user. Client records are preserved.
   *
   * @param id - Identity user id.
   * @returns True when deleted; false when refused (see `actionError`).
   */
  const deleteUser = (id: string): Promise<boolean> => mutate(() => api.remove(id))

  /**
   * Sends a password reset email to the user.
   *
   * @param id - Identity user id.
   * @returns True when sent; false when refused (see `actionError`).
   */
  const sendPasswordReset = (id: string): Promise<boolean> => mutate(() => api.sendPasswordReset(id))

  /**
   * Sets a new password for a user (admin action).
   *
   * @param id - Identity user id.
   * @param password - The new password.
   * @returns True when changed; false when refused (see `actionError`).
   */
  const changePassword = (id: string, password: string): Promise<boolean> =>
    mutate(() => api.changePassword(id, password))

  return {
    users,
    totalCount,
    pageSize,
    current,
    loading,
    error,
    actionError,
    fieldErrors,
    fetchAll,
    fetchById,
    updateUser,
    deleteUser,
    sendPasswordReset,
    changePassword,
  }
})
