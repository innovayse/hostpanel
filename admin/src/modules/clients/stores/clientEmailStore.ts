import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '../../../composables/useApi'
import type { EmailLog } from '../../../types/models'

/**
 * Manages email log state for a specific client profile page.
 *
 * Emails are lazy-loaded — call fetchClientEmails before reading emails.
 */
export const useClientEmailStore = defineStore('clientEmail', () => {
  const { request } = useApi()

  /** Fetched email log entries for the current client. */
  const emails = ref<EmailLog[]>([])

  /** Total number of matching email log entries. */
  const totalCount = ref(0)

  /** True while an API request is in flight. */
  const loading = ref(false)

  /** Human-readable error message, null when no error. */
  const error = ref<string | null>(null)

  /**
   * Fetches a paginated list of email logs sent to a specific client.
   *
   * @param clientId - The client's primary key.
   * @param page - 1-based page number.
   * @param pageSize - Number of entries per page.
   * @returns Promise that resolves when emails are loaded.
   */
  async function fetchClientEmails(clientId: number, page: number, pageSize: number): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const data = await request<{ items: EmailLog[]; totalCount: number }>(
        `/admin/email-logs/client/${clientId}?page=${page}&pageSize=${pageSize}`
      )
      emails.value = data.items
      totalCount.value = data.totalCount
    } catch {
      error.value = 'Failed to load emails'
    } finally {
      loading.value = false
    }
  }

  /**
   * Resets the store to its initial state.
   *
   * @returns void
   */
  function reset(): void {
    emails.value = []
    totalCount.value = 0
    loading.value = false
    error.value = null
  }

  return { emails, totalCount, loading, error, fetchClientEmails, reset }
})
