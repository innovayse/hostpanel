/**
 * Pinia store for the manual bank transfers that wait for staff: the list, and the two decisions (confirm, reject).
 *
 * It is the only caller of `useBankTransfersApi`; a refusal becomes the API sentence in `error`. A decision reloads the
 * current page, so a transfer that is no longer waiting (decided by someone else a moment ago) leaves the list.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useBankTransfersApi } from '@/composables/apis/useBankTransfersApi'
import { runAction } from '@/lib/runAction'
import type { PendingBankTransfer } from '@/types/pendingbanktransfer'

/** Lists the waiting bank transfers one page at a time and decides them. */
export const useBankTransfersStore = defineStore('bank-transfers', () => {
  /** The bank transfer endpoints; this store is their only caller. */
  const api = useBankTransfersApi()

  /** Waiting transfers of the current page. */
  const transfers = ref<PendingBankTransfer[]>([])

  /** Total number of waiting transfers, across all pages. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /**
   * Loads one page of waiting transfers.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Transfers per page.
   * @returns Promise that resolves when the page is loaded.
   */
  const fetchPage = async (page: number, pageSize: number): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.listPending(page, pageSize))
    loading.value = false
    if (result.ok) {
      transfers.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  /**
   * Confirms the transfer of an invoice.
   *
   * @param invoiceId - The invoice whose transfer arrived.
   * @returns True when confirmed; false when refused (see `error`).
   */
  const confirm = async (invoiceId: number): Promise<boolean> => {
    const result = await runAction(error, () => api.confirm(invoiceId))
    return result.ok
  }

  /**
   * Rejects the transfer of an invoice.
   *
   * @param invoiceId - The invoice whose transfer did not arrive.
   * @param note - The reason the client reads.
   * @returns True when rejected; false when refused (see `error`).
   */
  const reject = async (invoiceId: number, note: string): Promise<boolean> => {
    const result = await runAction(error, () => api.reject(invoiceId, note))
    return result.ok
  }

  return { transfers, totalCount, loading, error, fetchPage, confirm, reject }
})
