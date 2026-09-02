/**
 * The three questions the portal asks about an invoice that the API does not answer directly.
 *
 * `InvoiceDto` sends what it stores — a total, a due date, a status and a list of transactions
 * — and leaves the derivations to the reader. Those derivations were previously written inline
 * in each page, and each page got one of them wrong:
 *
 * - `pages/client/invoices/[id]/pay.vue` computed both money figures from `invoice.balance`,
 *   a field `InvoiceDto` has never had. `balance ?? total` therefore always took the second
 *   branch: **Payments to date rendered `0.00` on every invoice, including fully paid ones, and
 *   Balance Due rendered the full total on an invoice the customer had already part-paid** — on
 *   the screen that then asks them to pay it.
 * - `pages/client/invoices/index.vue` and `[id].vue` each had their own idea of "overdue", and
 *   only one of them knew the backend has an `Overdue` status of its own.
 *
 * One copy each, here, so a page cannot disagree with the page next to it about what a customer
 * owes. See `types/clientinvoice.ts` for the field vocabulary these read.
 *
 * @module utils/invoice
 */

import type { ClientInvoice, ClientInvoiceTransaction } from '~/types/clientinvoice'

/**
 * How much has actually been received against an invoice.
 *
 * A refund is recorded as a negative transaction amount, so a plain sum is the net received.
 * Gateway `fees` are the gateway's cut of a payment rather than part of what the customer
 * handed over, and are deliberately not subtracted.
 *
 * @param transactions - The invoice's recorded transactions; an absent list reads as none.
 * @returns The net amount received, in the account's billing currency.
 */
export const paymentsToDate = (transactions: ClientInvoiceTransaction[] | null | undefined): number =>
  (transactions ?? []).reduce((sum, txn) => sum + txn.amount, 0)

/**
 * What is still owed on an invoice: the total less everything received.
 *
 * @param invoice - The invoice, or nothing while one is still loading.
 * @returns The outstanding balance; `0` when there is no invoice to read.
 */
export const balanceDue = (invoice: ClientInvoice | null | undefined): number => {
  if (!invoice) return 0

  return invoice.total - paymentsToDate(invoice.transactions)
}

/**
 * Whether an invoice still has money owing on it.
 *
 * `Unpaid` and `Overdue` both mean the customer owes this; `Overdue` is only what `Unpaid`
 * becomes once `ProcessRenewalsCronHandler` has run over it. Reading one without the other
 * under-counts, and it under-counts the more urgent half.
 *
 * This exists because the dashboard had two answers on one screen: its stat card counted
 * `Unpaid` alone and its banner counted both, so the same account was shown "6 unpaid" beside
 * "You have 12 unpaid invoices". The card was the wrong one — it is the element that says
 * "Action required", and it was the element leaving out the overdue ones.
 *
 * @param invoice - The invoice to judge.
 * @returns True when the invoice is awaiting payment.
 */
export const isInvoiceOutstanding = (invoice: Pick<ClientInvoice, 'status'>): boolean =>
  invoice.status === 'Unpaid' || invoice.status === 'Overdue'

/**
 * Whether an invoice should be shown to the customer as late.
 *
 * Two states count. The backend has its own `Overdue` status, but nothing flips an invoice into
 * it at the moment the date passes — `ProcessRenewalsCronHandler` writes it — so an invoice can
 * sit `Unpaid` with a due date in the past until that next runs. Both are late on the calendar
 * the customer is looking at, which is the one that decides whether the date turns red.
 *
 * Every other status is not late, `Draft` and `Cancelled` included: an invoice that was never
 * issued or has been voided has nothing outstanding to be late about.
 *
 * @param invoice - The invoice, or nothing while one is still loading.
 * @param now - The moment to compare the due date against. Injectable so a test does not depend
 *              on the clock; defaults to the current time.
 * @returns True when the invoice is overdue.
 */
export const isInvoiceOverdue = (
  invoice: Pick<ClientInvoice, 'status' | 'dueDate'> | null | undefined,
  now: Date = new Date()
): boolean => {
  if (!invoice) return false
  if (invoice.status === 'Overdue') return true
  if (invoice.status !== 'Unpaid') return false

  const due = new Date(invoice.dueDate)
  // An unparseable due date is not evidence of lateness. Reading `Invalid Date < now` as false
  // is what the comparison already does; saying so stops it looking like an oversight.
  return Number.isFinite(due.getTime()) && due < now
}
