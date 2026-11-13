/**
 * Pure helpers of the invoice lists: the paid, due and overdue rollup the summary tiles show, built from the
 * rows a page already holds, and the narrowing of a page by the text typed in the search box.
 */
import { formatPlainAmount } from '@/lib/money'
import { CurrencyCode } from '@/types/currency'
import type { CurrencyAmount } from '@/types/currency'
import { InvoiceStatus } from '@/types/invoice'
import type { Invoice } from '@/types/invoice'

/** How many invoices of one kind a page holds and what they add up to, one sum per currency. */
interface InvoiceBucket {
  /** Number of invoices in the bucket. */
  count: number
  /** Sum of the invoice totals, one entry per currency; empty when the bucket is. */
  totals: CurrencyAmount[]
}

/** The rollup of one page of invoices. */
interface InvoiceListSummary {
  /** Invoices that are paid. */
  paid: InvoiceBucket
  /** Invoices that are issued and still owed but not yet late (unpaid, payment pending). */
  due: InvoiceBucket
  /** Invoices past their due date. */
  overdue: InvoiceBucket
}

/** The statuses that make an invoice "due": issued, owed, not yet late. */
const DUE_STATUSES: readonly string[] = [InvoiceStatus.Unpaid, InvoiceStatus.PaymentPending]

/**
 * Adds an invoice to a bucket, summing its total into the entry of its currency.
 *
 * @param bucket - The bucket to grow.
 * @param invoice - The invoice to count.
 * @returns Nothing; the bucket is changed.
 */
const addToBucket = (bucket: InvoiceBucket, invoice: Invoice): void => {
  const currency = invoice.currency || CurrencyCode.Usd
  bucket.count += 1
  const entry = bucket.totals.find(total => total.currency === currency)
  if (entry === undefined) {
    bucket.totals.push({ amount: invoice.total, currency })
  } else {
    entry.amount += invoice.total
  }
}

/**
 * Rolls a page of invoices up into paid, due and overdue.
 *
 * @param invoices - The invoices of the page.
 * @returns The count and the per-currency sum of each kind; other statuses are not counted.
 */
export const summarizeInvoices = (invoices: readonly Invoice[]): InvoiceListSummary => {
  const summary: InvoiceListSummary = {
    paid: { count: 0, totals: [] },
    due: { count: 0, totals: [] },
    overdue: { count: 0, totals: [] },
  }
  for (const invoice of invoices) {
    if (invoice.status === InvoiceStatus.Paid) {
      addToBucket(summary.paid, invoice)
    } else if (invoice.status === InvoiceStatus.Overdue) {
      addToBucket(summary.overdue, invoice)
    } else if (DUE_STATUSES.includes(invoice.status)) {
      addToBucket(summary.due, invoice)
    }
  }
  return summary
}

/**
 * Prints the sums of a bucket for a tile: one text per currency, joined, or a zero when there is none.
 *
 * @param bucket - The bucket to print.
 * @returns For example "100.00 USD" or "100.00 USD, 53160.00 AMD".
 */
export const bucketAmountText = (bucket: InvoiceBucket): string =>
  bucket.totals.length === 0
    ? formatPlainAmount(0)
    : bucket.totals.map(total => formatPlainAmount(total.amount, total.currency)).join(', ')

/**
 * Narrows a page of invoices by the search text: the invoice number, the client or the payment method.
 *
 * @param invoices - The invoices of the page.
 * @param term - The text typed in the search box; blank keeps every invoice.
 * @returns The invoices whose number, client name or payment method contains the text, ignoring case.
 */
export const searchInvoices = (invoices: readonly Invoice[], term: string): Invoice[] => {
  const needle = term.trim().toLowerCase()
  if (needle === '') {
    return [...invoices]
  }
  return invoices.filter(invoice =>
    [String(invoice.id), invoice.clientName, invoice.paymentMethod ?? '']
      .some(field => field.toLowerCase().includes(needle)),
  )
}
