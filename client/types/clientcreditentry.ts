/**
 * One line of the client's own credit history, as `GET /api/portal/client/credit` returns it
 * (`MyCreditEntryDto`): a movement of the balance and what it left.
 *
 * The line carries no description: a staff note is not written for the client, so the client reads the movement and the invoice.
 *
 * @module types/clientcreditentry
 */

import type { CreditMovement } from '~/types/creditmovement'

/** A movement of the credit balance. */
export interface ClientCreditEntry {
  /** The ledger entry that records the movement. */
  id: number
  /** When the balance moved (UTC, ISO 8601). */
  date: string
  /** Why it moved. */
  movement: `${CreditMovement}`
  /** The signed change: positive when credit was added, negative when it was taken. */
  change: number
  /** The balance once the change was saved. */
  balanceAfter: number
  /** The invoice the credit moved to or from; null when it belongs to none. */
  invoiceId: number | null
}
