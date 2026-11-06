/**
 * A client's credit as staff read it (`GET /api/clients/{id}/credit`, `ClientCreditDto`): the balance and a page of its history.
 */

import type { CreditAdjustment, CreditMovement } from '@/types/creditmovement'
import type { PaginatedList } from '@/types/paginatedlist'

/** One line of a client's credit history. */
export interface CreditEntry {
  /** The ledger entry that records the movement. */
  id: number
  /** ISO 8601 moment the balance moved. */
  date: string
  /** Why it moved. */
  movement: CreditMovement
  /** The signed change: positive when credit was added, negative when it was taken. */
  change: number
  /** The balance once the change was saved. */
  balanceAfter: number
  /** The invoice the credit moved to or from; null when it belongs to none. */
  invoiceId: number | null
  /** The ledger description, which carries the reason staff gave for an adjustment. */
  description: string
}

/** A client's credit balance with a page of its history, newest first. */
export interface ClientCredit {
  /** The client. */
  clientId: number
  /** The current credit balance, in `currency`. */
  balance: number
  /** ISO 4217 code the balance is held in. */
  currency: string
  /** A page of the history; movements older than the history are not in it. */
  entries: PaginatedList<CreditEntry>
}

/** The body of a staff adjustment (`POST /api/clients/{id}/credit/adjustments`). */
export interface AdjustCreditPayload {
  /** Whether credit is added or taken. */
  direction: CreditAdjustment
  /** The positive amount, in the currency the balance is held in. */
  amount: number
  /** Why the balance is adjusted; kept on the history line. */
  reason: string
}
