/**
 * Fixtures for the held payment claims screen: a list with two held claims, an empty list and the
 * refusal body of a failed load.
 */
import type { InvoicePaymentClaim } from '../../src/types/invoicepaymentclaim'
import { InvoicePaymentClaimStatus } from '../../src/types/invoicepaymentclaim'
import type { PaginatedList } from '../../src/types/paginatedlist'

/** Builds a held claim with the given identity. */
const heldClaim = (id: number, invoiceId: number, clientId: number, clientName: string, amount: number): InvoicePaymentClaim => ({
  id,
  invoiceId,
  clientId,
  clientName,
  gatewayModuleId: 'bank-gateway',
  amount,
  currency: 'USD',
  status: InvoicePaymentClaimStatus.Held,
  transactionId: `tx-${id}`,
  createdAt: '2026-10-14T09:00:00Z',
  resolvedAt: '2026-10-14T09:01:00Z',
})

/** `GET /api/billing/payment-claims`: two held claims. */
export const heldClaimsPage: PaginatedList<InvoicePaymentClaim> = {
  items: [heldClaim(3, 5, 9, 'Ada Lovelace', 12.5), heldClaim(4, 6, 10, 'Grace Hopper', 40)],
  pageNumber: 1,
  pageSize: 20,
  totalCount: 2,
  totalPages: 1,
}

/** `GET /api/billing/payment-claims`: nothing is held. */
export const emptyClaimsPage: PaginatedList<InvoicePaymentClaim> = {
  items: [],
  pageNumber: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
}

/** The sentence the refused load carries in its `detail`. */
export const claimsRefusalSentence = 'The payment claims are not available.'

/** Problem body of a refused load. */
export const claimsRefusal = { type: 'FORBIDDEN', title: 'Forbidden', status: 403, detail: claimsRefusalSentence }
