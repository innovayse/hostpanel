/**
 * Pinia store for the e-mail log of one client profile.
 *
 * Lazy-loaded: call `fetchClientEmails` before reading `emails`. It reads through
 * `useClientHistoryApi`; a refusal becomes the API sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientHistoryApi } from '@/composables/apis/useClientHistoryApi'
import { DEFAULT_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { EmailLog, EmailLogFilters } from '@/types/emaillog'

/** Loads the e-mails sent to a client. */
export const useClientEmailStore = defineStore('clientEmail', () => {
  const api = useClientHistoryApi()

  /** Fetched e-mail log entries for the current client. */
  const emails = ref<EmailLog[]>([])

  /** Total number of matching e-mail log entries. */
  const totalCount = ref(0)

  /** Items per page. */
  const pageSize = ref(DEFAULT_PAGE_SIZE)

  /** True while an API request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed load, null when none. */
  const error = ref<string | null>(null)

  /**
   * Fetches one page of the e-mails sent to a client, narrowed by the server. The page, the search, the outcome and the
   * period come from the list's address, so the store keeps no copy of them.
   *
   * @param clientId - The client primary key.
   * @param page - 1-based page number.
   * @param filters - The search text, the outcome and the period; a member left out does not narrow.
   * @returns Promise that resolves when e-mails are loaded.
   */
  const fetchClientEmails = async (clientId: number, page: number, filters: EmailLogFilters = {}): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () =>
      api.emailLogs(clientId, page, pageSize.value, { ...filters, search: filters.search?.trim() }),
    )
    loading.value = false
    if (result.ok) {
      emails.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Resets the store to its initial state.
   *
   * @returns Nothing.
   */
  const reset = (): void => {
    emails.value = []
    totalCount.value = 0
    loading.value = false
    error.value = null
  }

  return { emails, totalCount, pageSize, loading, error, fetchClientEmails, reset }
})
