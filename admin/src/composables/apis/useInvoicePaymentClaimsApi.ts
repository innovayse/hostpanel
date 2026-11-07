/**
 * One function per endpoint on the API invoice payment claims surface (`/api/billing/payment-claims`).
 * No state lives here, no error handling.
 *
 * The caller is the invoice payment claims store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import type { PaginatedList } from '@/types/paginatedlist'
import type { InvoicePaymentClaim, InvoicePaymentClaimStatus } from '@/types/invoicepaymentclaim'

/**
 * Endpoint functions for the invoice payment claims.
 *
 * @returns One typed function per claims endpoint the admin panel uses.
 */
export const useInvoicePaymentClaimsApi = () => {
  /** Transport call of the shared API module. */
  const { request } = useApi()

  /**
   * Lists one page of payment claims, newest first.
   *
   * @param status - Status to keep, or undefined for claims of every status (not sent).
   * @param page - Page number (1-based).
   * @param pageSize - Claims per page.
   * @returns One page of claims.
   */
  const list = (
    status: InvoicePaymentClaimStatus | undefined,
    page: number,
    pageSize: number,
  ): Promise<PaginatedList<InvoicePaymentClaim>> =>
    request<PaginatedList<InvoicePaymentClaim>>(`/billing/payment-claims${buildQuery({ page, pageSize, status })}`)

  return { list }
}
