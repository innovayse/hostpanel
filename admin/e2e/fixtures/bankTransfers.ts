/**
 * Fixtures for the pending bank transfers screen: a page with two waiting transfers, an empty page and the refusal
 * body of a failed load.
 */
import type { PaginatedList } from '../../src/types/paginatedlist'
import type { PendingBankTransfer } from '../../src/types/pendingbanktransfer'

/** Builds a waiting transfer with the given identity. */
const waitingTransfer = (invoiceId: number, clientId: number, clientName: string, amountDue: number): PendingBankTransfer => ({
  invoiceId,
  reference: `INV-${invoiceId}`,
  clientId,
  clientName,
  total: amountDue,
  amountDue,
  currency: 'USD',
  dueDate: '2026-10-30T00:00:00Z',
  requestedAt: '2026-10-14T09:00:00Z',
})

/** `GET /api/billing/bank-transfers/pending`: two transfers wait. */
export const waitingTransfers: PendingBankTransfer[] = [
  waitingTransfer(5, 9, 'Ada Lovelace', 100),
  waitingTransfer(6, 10, 'Grace Hopper', 40),
]

/**
 * Wraps transfers in the list envelope.
 *
 * @param items - The transfers on the page.
 * @returns The page the endpoint answers with.
 */
export const transfersPage = (items: PendingBankTransfer[]): PaginatedList<PendingBankTransfer> => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: items.length === 0 ? 0 : 1,
})

/** The sentence the refused load carries in its `detail`. */
export const transfersRefusalSentence = 'The pending transfers are not available.'

/** Problem body of a refused load. */
export const transfersRefusal = { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: transfersRefusalSentence }
