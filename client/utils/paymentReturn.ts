/**
 * Reads the parameters a gateway's return request carried, for the backend to hand to the payment plugin.
 *
 * A gateway returns the payer to the result page with what the payer's browser produced (a payment
 * token, a payer id) in the query. The page's own parameters name the order or invoice and are not the
 * gateway's, so they are left out. Every other pair is passed in the order it arrived, a repeated name
 * included: which value wins, and how many parameters a plugin may receive, is the backend's rule, not
 * this page's.
 *
 * @module utils/paymentReturn
 */

import type { PaymentReturnParameter } from '~/types/paymentreturnparameter'

/** The query names the result page reads itself; they are not part of the gateway's return. */
const PAGE_PARAMETERS: ReadonlySet<string> = new Set(['order', 'invoice'])

/**
 * Lists the gateway's parameters of a route query.
 *
 * @param query - The route's query: a string, a repeated key (an array) or a key without a value (null).
 * @returns The names and values in the order they arrived, without the page's own `order` and `invoice`.
 */
export const returnParametersOf = (query: Record<string, unknown>): PaymentReturnParameter[] =>
  Object.entries(query)
    .filter(([name]) => !PAGE_PARAMETERS.has(name))
    .flatMap(([name, raw]) =>
      (Array.isArray(raw) ? raw : [raw]).map(value => ({ name, value: typeof value === 'string' ? value : '' })))
