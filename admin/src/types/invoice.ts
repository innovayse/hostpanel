/**
 * An invoice, its lines, its payments and the status rollup the lists show.
 * 
 * Kept together because none of them is meaningful alone: a line belongs to an invoice, a
 * transaction settles one, and the count is what a screen renders above a list of them.
 */

import type { Gateway } from './gateway'
import type { PagedResult } from './pagedresult'
import type { Transaction } from './transaction'

/** Single line item on an invoice. */
export interface InvoiceItem {
  /** Unique line item identifier. */
  id: number
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Number of units. */
  quantity: number
  /** Line total (unitPrice x quantity). */
  amount: number

  /**
   * BUG — read by `InvoiceDetailView`'s "Taxed" column, but `InvoiceItemDto` has no such
   * field, so the column has always rendered "No" for every line regardless of tax.
   *
   * Declared optional so the type states what actually arrives (`undefined`) instead of
   * promising a boolean. Whether tax is per-line or invoice-wide (`Invoice.taxRate`) is a
   * product decision; nothing here can infer it.
   */
  taxed?: boolean
}

/** Payment, refund, or credit transaction recorded against an invoice. */
export interface InvoiceTransaction {
  /** Unique transaction identifier. */
  id: number
  /** Transaction date (ISO 8601). */
  date: string
  /** Payment gateway used. */
  gateway: string
  /** Gateway transaction reference. */
  transactionId: string
  /** Transaction amount. */
  amount: number
  /** Transaction fees charged by the gateway. */
  fees: number
  /** Transaction type: Payment, Refund, or Credit. */
  type: string
  /** Optional admin notes. */
  notes?: string
}

/** Represents an invoice. */
export interface Invoice {
  /** Unique invoice identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Invoice status (Draft, Unpaid, Paid, Overdue, Cancelled, Refunded, Collections, PaymentPending). */
  status: string
  /** ISO 8601 due date. */
  dueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** Total amount due. */
  total: number
  /** Optional admin notes. */
  notes?: string
  /** ISO 8601 invoice date. */
  invoiceDate: string
  /** Payment method label. */
  paymentMethod?: string
  /** Tax rate percentage. */
  taxRate: number
  /** Computed tax amount. */
  tax: number
  /** Sub-total before tax and credit. */
  subTotal: number
  /** Credit applied to the invoice. */
  credit: number
  /** ISO 8601 payment timestamp, if paid. */
  paidAt?: string
  /** Gateway transaction reference, if paid. */
  gatewayTransactionId?: string
  /** Display name of the owning client. */
  clientName: string
  /** Line items on the invoice. */
  items: InvoiceItem[]
  /** Payment/refund/credit transactions. */
  transactions: InvoiceTransaction[]

  /**
   * BUG — read by `InvoiceDetailView` (the "Payment Method" summary field and the payment
   * panel), but `InvoiceDto` sends no `gateway`. Both call sites therefore show their
   * fallback — '' and 'Not set' — on every invoice, paid or not.
   *
   * The API's nearest equivalents are {@link Invoice.paymentMethod} and
   * {@link Invoice.gatewayTransactionId}. Switching to one of those changes what the screen
   * displays, so it is left as-is and recorded here.
   */
  gateway?: string

  /**
   * BUG — read by `InvoicesListView`'s "Last Updated" column as
   * `invoice.updatedAt || invoice.createdAt`, but `InvoiceListItemDto` has no `updatedAt`.
   * The column has always shown the creation date.
   */
  updatedAt?: string
}

/** Invoice count and total for a single status. */
export interface InvoiceStatusCount {
  /** Number of invoices in this status. */
  count: number
  /** Sum of totals for invoices in this status. */
  total: number
}
