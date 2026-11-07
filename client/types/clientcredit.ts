/**
 * The client's own credit, as `GET /api/portal/client/credit` returns it (`MyCreditDto`): the balance, the limits of one top-up
 * and a page of the history, newest first.
 *
 * @module types/clientcredit
 */

import type { ClientCreditEntry } from '~/types/clientcreditentry'
import type { PaginatedList } from '~/types/paginatedlist'

/** The client's credit. */
export interface ClientCredit {
  /** The current credit balance, in `currency`. */
  balance: number
  /** ISO 4217 code the balance and the top-up limits are in: the client's billing currency. */
  currency: string
  /** The least the client may add in one top-up. */
  topUpMinimum: number
  /** The most the client may add in one top-up. */
  topUpMaximum: number
  /** A page of the history; movements older than the history are not in it. */
  entries: PaginatedList<ClientCreditEntry>
}
