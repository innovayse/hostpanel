/**
 * Pinia store for the unsubscribe landing page: where the attempt stands and, when the API
 * refused the link, its sentence.
 *
 * URLs and transport belong to {@link useUnsubscribeApi}; this store owns only the state.
 *
 * @module stores/unsubscribe
 */

import { defineStore } from 'pinia'
import { useUnsubscribeApi } from '~/composables/apis/useUnsubscribeApi'
import { apiErrorMessage } from '~/lib/errorHandler'
import { UnsubscribeStatus } from '~/types/unsubscribestatus'

/**
 * Central store for the unsubscribe page.
 *
 * @returns The attempt's status, the refusal sentence and the action that makes the attempt.
 */
export const useUnsubscribeStore = defineStore('unsubscribe', () => {
  const api = useUnsubscribeApi()

  /** Where the attempt stands; drives the landing page's panels. Idle until the reader confirms. */
  const status = ref<UnsubscribeStatus>(UnsubscribeStatus.Idle)

  /** The sentence for a refused link; empty until the API refuses one. */
  const error = ref('')

  /**
   * Unsubscribes the address behind a mail's link.
   *
   * An empty token is sent as it is: the API refuses it with its own sentence, in the reader's language.
   *
   * @param token - Token from the link, empty when missing.
   * @returns The resulting {@link UnsubscribeStatus}; on `Failed`, {@link error} holds the sentence to show.
   */
  const unsubscribe = async (token: string): Promise<UnsubscribeStatus> => {
    status.value = UnsubscribeStatus.Pending
    error.value = ''
    try {
      await api.unsubscribe(token)
      status.value = UnsubscribeStatus.Done
    } catch (e: unknown) {
      error.value = apiErrorMessage(e)
      status.value = UnsubscribeStatus.Failed
    }
    return status.value
  }

  return { status, error, unsubscribe }
})
