/**
 * A payment transaction and the paged envelope it arrives in.
 */

import type { Gateway } from './gateway'
import type { PagedResult } from './pagedresult'

/** Client-level financial ledger entry. */
export interface Transaction {
  /** Unique transaction identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** ISO 8601 transaction date. */
  date: string
  /** Human-readable description of the transaction. */
  description: string
  /** External gateway transaction reference. */
  transactionId: string
  /** Optional linked invoice identifier. */
  invoiceId: number | null
  /** Payment method used (e.g. Stripe, Manual). */
  paymentMethod: string
  /** Amount received. */
  amountIn: number
  /** Amount sent out. */
  amountOut: number
  /** Gateway fees. */
  fees: number
  /** Whether this transaction affected client credit balance. */
  addedToCredit: boolean
}

/** Response from the transactions list endpoint with summary totals. */
export interface TransactionsResult {
  /** Paginated transaction list. */
  transactions: PagedResult<Transaction>
  /** Sum of all AmountIn values. */
  totalIn: number
  /** Sum of all AmountOut values. */
  totalOut: number
  /** Sum of all Fees values. */
  totalFees: number
  /** Calculated balance: TotalIn - TotalOut - TotalFees. */
  balance: number
}
