/**
 * Pinia store for the invoice payment claims list (the held charges an operator settles by hand).
 *
 * It is the only caller of `useInvoicePaymentClaimsApi`; a refusal becomes the API sentence in `error`.
 */

import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useInvoicePaymentClaimsApi } from '@/composables/apis/useInvoicePaymentClaimsApi'
import { runAction } from '@/lib/runAction'
import type { InvoicePaymentClaim, InvoicePaymentClaimStatus } from '@/types/invoicepaymentclaim'

/** Lists payment claims one page at a time. */
export const useInvoicePaymentClaimsStore = defineStore('invoice-payment-claims', () => {
  /** The claims endpoints; this store is their only caller. */
  const api = useInvoicePaymentClaimsApi()

  /** Claims of the current page. */
  const claims = ref<InvoicePaymentClaim[]>([])

  /** Total number of claims matching the status, across all pages. */
  const totalCount = ref(0)

  /** True while a request is in flight. */
  const loading = ref(false)

  /** Sentence for the last failed request, null when none. */
  const error = ref<string | null>(null)

  /**
   * Loads one page of claims.
   *
   * @param status - Status to keep, or undefined for every status.
   * @param page - Page number (1-based).
   * @param pageSize - Claims per page.
   * @returns Promise that resolves when the page is loaded.
   */
  const fetchPage = async (status: InvoicePaymentClaimStatus | undefined, page: number, pageSize: number): Promise<void> => {
    loading.value = true
    const result = await runAction(error, () => api.list(status, page, pageSize))
    loading.value = false
    if (result.ok) {
      claims.value = result.value.items
      totalCount.value = result.value.totalCount
    }
  }

  return { claims, totalCount, loading, error, fetchPage }
})
