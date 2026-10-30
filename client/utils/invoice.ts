/**
 * The questions the portal asks about an invoice that the API does not answer directly.
 *
 * `MyInvoiceDto` sends what it stores (a total, a due date, a status and a list of transactions)
 * and leaves the derivations to the reader:
 *
 * - Payments to date come from the transactions, and Balance Due is the API's own `amountDue`;
 *   falling back to `total` would show a part-paid invoice as owing its full total on the screen
 *   that asks the customer to pay it.
 * - "Overdue" includes the backend's own `Overdue` status, not only a past due date.
 *
 * One copy each, here, so a page cannot disagree with the page next to it about what a customer
 * owes. See `types/clientinvoice.ts` for the field vocabulary these read.
 *
 * @module utils/invoice
 */

import type { ClientInvoice } from '~/types/clientinvoice'
import type { ClientInvoiceTransaction } from '~/types/clientinvoicetransaction'
import { InvoiceStatus } from '~/types/invoicestatus'
import { InvoiceTransactionType } from '~/types/invoicetransactiontype'

/**
 * How much has been paid towards an invoice: the sum of its `Payment` transactions.
 *
 * Mirrors the backend's `Invoice.PaymentsRecorded`. Every amount is stored positive and the
 * transaction type says which way the money moved, so a plain sum would count a refund as money
 * received. Refunds and credits are left out: a credit is already netted out of the total, and a
 * refund returns money without reopening the invoice.
 *
 * @param transactions - The invoice's recorded transactions; an absent list reads as none.
 * @returns The amount paid, in the invoice's currency.
 */
export const paymentsToDate = (transactions: ClientInvoiceTransaction[] | null | undefined): number =>
  (transactions ?? [])
    .filter((txn) => txn.type === InvoiceTransactionType.Payment)
    .reduce((sum, txn) => sum + txn.amount, 0)

/**
 * What is still owed on an invoice, as the API computed it (`MyInvoiceDto.AmountDue`).
 *
 * The server's figure, not one worked out here: it is the amount a payment started from the
 * portal charges, so the page shows the customer exactly what they will pay, and a part-paid
 * invoice that stays open shows only what is left on it.
 *
 * @param invoice - The invoice, or nothing while one is still loading.
 * @returns The outstanding balance; `0` when there is no invoice to read.
 */
export const balanceDue = (invoice: ClientInvoice | null | undefined): number => invoice?.amountDue ?? 0

/**
 * Whether an invoice still has money owing on it.
 *
 * `Unpaid` and `Overdue` both mean the customer owes this; `Overdue` is only what `Unpaid`
 * becomes once `ProcessRenewalsCronHandler` has run over it. Reading one without the other
 * under-counts, and it under-counts the more urgent half.
 *
 * One definition keeps the dashboard's stat card and its banner from showing two different
 * unpaid counts for the same account.
 *
 * @param invoice - The invoice to judge.
 * @returns True when the invoice is awaiting payment.
 */
export const isInvoiceOutstanding = (invoice: Pick<ClientInvoice, 'status'>): boolean =>
  invoice.status === InvoiceStatus.Unpaid || invoice.status === InvoiceStatus.Overdue

/**
 * Whether an invoice has been paid and still counts as paid.
 *
 * Mirrors the backend's `Invoice.PaidStatuses`: `Paid`, and `PartiallyRefunded`, which is a paid
 * invoice of which part was given back. Both settle the invoice, so both belong on the paid tab and
 * neither can be paid again; a fully `Refunded` invoice holds none of the money and is not paid.
 *
 * @param invoice - The invoice to judge.
 * @returns True when the invoice is paid.
 */
export const isInvoicePaid = (invoice: Pick<ClientInvoice, 'status'>): boolean =>
  invoice.status === InvoiceStatus.Paid || invoice.status === InvoiceStatus.PartiallyRefunded

/**
 * Whether an invoice should be shown to the customer as late.
 *
 * Two states count. The backend has its own `Overdue` status, but nothing flips an invoice into
 * it at the moment the date passes (`ProcessRenewalsCronHandler` writes it), so an invoice can
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
  if (!invoice) {
    return false
  }
  if (invoice.status === InvoiceStatus.Overdue) {
    return true
  }
  if (invoice.status !== InvoiceStatus.Unpaid) {
    return false
  }

  const due = new Date(invoice.dueDate)
  // An unparseable due date is not evidence of lateness. Reading `Invalid Date < now` as false
  // is what the comparison already does; saying so stops it looking like an oversight.
  return Number.isFinite(due.getTime()) && due < now
}
