import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { PagedResult } from '../../../types/models'

/** Summary DTO for user list rows. */
export interface UserListItem {
  /** Identity user ID. */
  id: string
  /** Linked client ID, or null if no client record. */
  clientId: number | null
  /** User first name. */
  firstName: string
  /** User last name. */
  lastName: string
  /** User email address. */
  email: string
  /** Preferred language code or null. */
  language: string | null
  /** Last login timestamp or null. */
  lastLoginAt: string | null
  /** Account creation timestamp. */
  createdAt: string
}

/** Client account linked to a user. */
export interface UserAccount {
  /** Client primary key. */
  clientId: number
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Company name or null. */
  companyName: string | null
  /** Whether this user owns the account. */
  isOwner: boolean
}

/** Full user detail with linked accounts. */
export interface UserDetail extends UserListItem {
  /** Linked client accounts. */
  accounts: UserAccount[]
}

/**
 * Pinia store for admin user management.
 *
 * Handles paginated list, single-user detail, CRUD, and password operations.
 */
export const useUsersStore = defineStore('users', () => {
  const { request } = useApi()

  /** Loaded users list. */
  const users = ref<UserListItem[]>([])

  /** Total users matching the current query. */
  const totalCount = ref(0)

  /** Current page number (1-based). */
  const page = ref(1)

  /** Items per page. */
  const pageSize = ref(20)

  /** Currently viewed user detail. */
  const current = ref<UserDetail | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches a paginated user list.
   *
   * @param search - Optional search term.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchAll(search?: string): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({
        page: page.value.toString(),
        pageSize: pageSize.value.toString(),
      })
      if (search) params.set('search', search)
      const result = await request<PagedResult<UserListItem>>(`/admin/users?${params}`)
      users.value = result.items
      totalCount.value = result.totalCount
    } catch {
      error.value = 'Failed to load users.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Fetches a single user with linked accounts.
   *
   * @param id - Identity user ID.
   * @returns Promise that resolves when data is loaded.
   */
  async function fetchById(id: string): Promise<void> {
    loading.value = true
    error.value = null
    current.value = null
    try {
      current.value = await request<UserDetail>(`/admin/users/${id}`)
    } catch {
      error.value = 'Failed to load user details.'
    } finally {
      loading.value = false
    }
  }

  /**
   * Updates a user's profile.
   *
   * @param id - Identity user ID.
   * @param data - Updated profile fields.
   * @returns Promise that resolves on success.
   */
  async function updateUser(id: string, data: { firstName: string; lastName: string; email: string; language: string | null }): Promise<void> {
    await request(`/admin/users/${id}`, { method: 'PUT', body: JSON.stringify(data) })
  }

  /**
   * Deletes a user. Client records are preserved.
   *
   * @param id - Identity user ID.
   * @returns Promise that resolves on success.
   */
  async function deleteUser(id: string): Promise<void> {
    await request(`/admin/users/${id}`, { method: 'DELETE' })
  }

  /**
   * Sends a password reset email to the user.
   *
   * @param id - Identity user ID.
   * @returns Promise that resolves on success.
   */
  async function sendPasswordReset(id: string): Promise<void> {
    await request(`/admin/users/${id}/reset-password`, { method: 'POST' })
  }

  /**
   * Sets a new password for a user (admin action).
   *
   * @param id - Identity user ID.
   * @param password - The new password.
   * @returns Promise that resolves on success.
   */
  async function changePassword(id: string, password: string): Promise<void> {
    await request(`/admin/users/${id}/change-password`, { method: 'POST', body: JSON.stringify({ password }) })
  }

  return {
    users, totalCount, page, pageSize, current, loading, error,
    fetchAll, fetchById, updateUser, deleteUser, sendPasswordReset, changePassword,
  }
})
