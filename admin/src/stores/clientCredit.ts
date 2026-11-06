/**
 * Pinia store for one client's credit on the Credit tab: the balance, a page of the history, and the adjustments staff make.
 *
 * It is the only caller of `useClientCreditApi`; a refusal becomes the API sentence in `error` (and the per-field messages in
 * `fieldErrors`). An adjustment reloads the first page, so the new line and the new balance are on screen at once.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useClientCreditApi } from '@/composables/apis/useClientCreditApi'
import { LEDGER_PAGE_SIZE } from '@/lib/limits'
import { runAction } from '@/lib/runAction'
import type { ClientCredit } from '@/types/clientcredit'
import type { CreditAdjustment } from '@/types/creditmovement'

/** Loads a client's credit and adjusts it. */
export const useClientCreditStore = defineStore('clientCredit', () => {
  /** The credit endpoints; this store is their only caller. */
  const api = useClientCreditApi()

  /** The credit on screen; null until it is loaded or after a refused load. */
  const credit = ref<ClientCredit | null>(null)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /** Per-field messages of the last refused adjustment. */
  const fieldErrors = ref<Record<string, string>>({})

  /**
   * Loads one page of a client's credit.
   *
   * @param clientId - The client.
   * @param page - Page number (1-based).
   * @returns True when loaded; false when refused (see `error`).
   */
  const fetchCredit = async (clientId: number | string, page = 1): Promise<boolean> => {
    loading.value = true
    const result = await runAction(error, () => api.fetchCredit(clientId, page, LEDGER_PAGE_SIZE))
    loading.value = false
    if (result.ok) {
      credit.value = result.value
    }
    return result.ok
  }

  /**
   * Adds credit to the client's balance or takes it off, then reloads the first page.
   *
   * @param clientId - The client.
   * @param direction - Whether credit is added or taken.
   * @param amount - The positive amount.
   * @param reason - Why the balance is adjusted.
   * @returns True when adjusted; false when refused (see `error` and `fieldErrors`).
   */
  const adjust = async (clientId: number | string, direction: CreditAdjustment, amount: number, reason: string): Promise<boolean> => {
    const result = await runAction(error, () => api.adjust(clientId, { direction, amount, reason }), fieldErrors)
    if (!result.ok) {
      return false
    }
    return fetchCredit(clientId, 1)
  }

  return { credit, loading, error, fieldErrors, fetchCredit, adjust }
})
