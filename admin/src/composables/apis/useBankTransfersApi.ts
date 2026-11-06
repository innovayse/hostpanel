/**
 * One function per endpoint on the manual bank transfer surface (`/api/billing/bank-transfers/pending` and
 * `/api/billing/{id}/bank-transfer/{confirm|reject}`). No state lives here, no error handling.
 *
 * The caller is the bank transfers store, never a `.vue` file.
 */

import { buildQuery, useApi } from '@/composables/useApi'
import { ApiPath } from '@/lib/apiPaths'
import { HttpMethod } from '@/types/http'
import type { PaginatedList } from '@/types/paginatedlist'
import type { PendingBankTransfer } from '@/types/pendingbanktransfer'

/**
 * Endpoint functions for the manual bank transfers.
 *
 * @returns One typed function per bank transfer endpoint the admin panel uses.
 */
export const useBankTransfersApi = () => {
  /** Transport call of the shared API module. */
  const { request } = useApi()

  /**
   * Lists one page of the transfers that wait for a decision, the one waiting longest first.
   *
   * @param page - Page number (1-based).
   * @param pageSize - Transfers per page.
   * @returns One page of waiting transfers.
   */
  const listPending = (page: number, pageSize: number): Promise<PaginatedList<PendingBankTransfer>> =>
    request<PaginatedList<PendingBankTransfer>>(`${ApiPath.Billing}/bank-transfers/pending${buildQuery({ page, pageSize })}`)

  /**
   * Confirms that the transfer of an invoice arrived; the invoice is paid through the normal payment path.
   *
   * @param invoiceId - The invoice whose transfer arrived.
   * @returns Nothing.
   */
  const confirm = (invoiceId: number): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${invoiceId}/bank-transfer/confirm`, { method: HttpMethod.Post })

  /**
   * Rejects the transfer of an invoice; the invoice is open again and the client reads the note.
   *
   * @param invoiceId - The invoice whose transfer did not arrive.
   * @param note - The reason the client reads.
   * @returns Nothing.
   */
  const reject = (invoiceId: number, note: string): Promise<void> =>
    request<void>(`${ApiPath.Billing}/${invoiceId}/bank-transfer/reject`, {
      method: HttpMethod.Post,
      body: JSON.stringify({ note }),
    })

  return { listPending, confirm, reject }
}
