/**
 * An invoice, its lines, its payments and the status rollup the lists show.
 * 
 * Kept together because none of them is meaningful alone: a line belongs to an invoice, a
 * transaction settles one, and the count is what a screen renders above a list of them.
 */


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

/** The dates of one invoice. */
export interface InvoiceDates {
  /** ISO 8601 invoice date. */
  invoiceDate: string
  /** ISO 8601 due date. */
  dueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** ISO 8601 payment timestamp, null when never paid. */
  paidAt: string | null
}

/** The money figures of one invoice, in the invoice's own currency. */
export interface InvoiceAmounts {
  /** Sub-total before tax and credit. */
  subTotal: number
  /** Credit applied to the invoice. */
  credit: number
  /** Computed tax amount. */
  tax: number
  /** Tax rate percentage. */
  taxRate: number
  /** Total amount due. */
  total: number
}

/** How one invoice is, or is to be, paid. */
export interface InvoicePayment {
  /** Payment method label, null when none is set. */
  method: string | null
  /** Gateway transaction reference, null while unpaid. */
  gatewayTransactionId: string | null
}

/** One invoice as the detail screen reads it (`GET /api/billing/{id}`). */
export interface InvoiceDetail {
  /** Unique invoice identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Display name of the owning client; every invoice has one, since its client cannot be deleted. */
  clientName: string
  /** Invoice status (Draft, Unpaid, Paid, Overdue, Cancelled, Refunded, Collections, PaymentPending, PartiallyRefunded). */
  status: string
  /** ISO 4217 code of the currency the invoice is issued in. */
  currency: string
  /** Invoice, due, creation and payment dates. */
  dates: InvoiceDates
  /** Sub-total, credit, tax, tax rate and total. */
  amounts: InvoiceAmounts
  /** What is still owed, as the API computes it: the total less the payments recorded; 0 for Paid, PartiallyRefunded, Refunded or Cancelled. */
  amountDue: number
  /** Payment method and gateway reference. */
  payment: InvoicePayment
  /** Optional admin notes. */
  notes: string | null
  /** Line items on the invoice. */
  items: InvoiceItem[]
  /** Payment/refund/credit transactions. */
  transactions: InvoiceTransaction[]
}

/** One row of an invoice list (`GET /api/billing`). */
export interface Invoice {
  /** Unique invoice identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Display name of the owning client; every invoice has one, since its client cannot be deleted. */
  clientName: string
  /** Invoice status (Draft, Unpaid, Paid, Overdue, Cancelled, Refunded, Collections, PaymentPending, PartiallyRefunded). */
  status: string
  /** ISO 8601 due date. */
  dueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** ISO 8601 invoice date. */
  invoiceDate: string
  /** ISO 8601 payment timestamp, if paid. */
  paidAt?: string
  /** Total amount due. */
  total: number
  /** Sub-total before tax and credit. */
  subTotal: number
  /** Computed tax amount. */
  tax: number
  /** Payment method label. */
  paymentMethod?: string
  /** ISO 4217 code of the currency the invoice is issued in; absent on an answer that does not carry it. */
  currency?: string
}

/** Invoice count and total for a single status. */
export interface InvoiceStatusCount {
  /** Number of invoices in this status. */
  count: number
  /** Sum of totals for invoices in this status. */
  total: number
}

/** Filters the invoice list endpoints accept; an empty value is left off the request. */
export interface InvoiceFilters {
  /** Status filter, e.g. `Unpaid`. */
  status?: string
  /** Start date filter (ISO 8601). */
  from?: string
  /** End date filter (ISO 8601). */
  to?: string
}

/** One line of a new invoice. */
export interface InvoiceItemInput {
  /** Human-readable charge description. */
  description: string
  /** Price per unit. */
  unitPrice: number
  /** Number of units. */
  quantity: number
}

/** Body of `POST /billing`. */
export interface CreateInvoicePayload {
  /** The client to invoice. */
  clientId: number
  /** ISO 8601 due date. */
  dueDate: string
  /** Line items to include. */
  items: InvoiceItemInput[]
  /** True to create as draft, false to create unpaid. */
  isDraft: boolean
}

/**
 * One entry of `PUT /billing/{id}/items`, mirroring the API UpdateItemEntryRequest:
 * `id` is null for a new line, and `isDeleted` removes an existing one.
 */
export interface InvoiceItemUpdate extends InvoiceItemInput {
  /** Existing line item id; null for a new line. */
  id: number | null
  /** When true, the line is removed from the invoice. */
  isDeleted: boolean
}

/** Body of `PUT /billing/{id}/options`. */
export interface InvoiceOptionsPayload {
  /** ISO 8601 invoice date. */
  invoiceDate: string
  /** ISO 8601 due date. */
  dueDate: string
  /** Payment method label. */
  paymentMethod: string
  /** Tax rate percentage. */
  taxRate: number
}

/** Body of `POST /billing/{id}/payment`. */
export interface InvoicePaymentPayload {
  /** ISO 8601 payment date. */
  date: string
  /** Payment gateway used. */
  gateway: string
  /** Gateway transaction reference. */
  transactionId: string
  /** Amount paid. */
  amount: number
  /** Gateway fees, when any. */
  fees?: number
  /** Admin notes, when any. */
  notes?: string
}

/** Body of `POST /billing/{id}/refund`. */
export interface InvoiceRefundPayload {
  /** Amount to refund; greater than 0 and at most what is still refundable. */
  amount: number
  /** Kind of refund. */
  refundType: InvoiceRefundType
  /** Payment method recorded for a Manual refund; left off for the other kinds. */
  gateway?: string
  /** External reference of a Manual refund. */
  refundTransactionId?: string
  /** Admin notes, when any. */
  notes?: string
}

/** Identifier the API answers a create or duplicate with. */
export interface CreatedId {
  /** The new record id. */
  id: number
}

/** Status of an invoice, exactly as the API sends it. */
export enum InvoiceStatus {
  Draft = 'Draft',
  Unpaid = 'Unpaid',
  Paid = 'Paid',
  Overdue = 'Overdue',
  Cancelled = 'Cancelled',
  Refunded = 'Refunded',
  Collections = 'Collections',
  PaymentPending = 'PaymentPending',
  /** Paid, and part of the money refunded; the rest can still be refunded. */
  PartiallyRefunded = 'PartiallyRefunded',
}

/** Kinds of transaction recorded against an invoice. */
export enum InvoiceTransactionType {
  Payment = 'Payment',
  Refund = 'Refund',
  Credit = 'Credit',
}

/** Refund kinds the API understands. */
export enum InvoiceRefundType {
  Gateway = 'Gateway',
  Manual = 'Manual',
  CreditBalance = 'CreditBalance',
}

/** Actions `POST /billing/bulk` applies to several invoices at once. */
export enum InvoiceBulkAction {
  MarkPaid = 'MarkPaid',
  MarkUnpaid = 'MarkUnpaid',
  MarkCancelled = 'MarkCancelled',
  Duplicate = 'Duplicate',
  Delete = 'Delete',
}

/** Closed set of ways an invoice detail screen is opened (the route action). */
export const InvoiceOpenAction = {
  /** Read-only view. */
  View: 'view',
  /** Editable. */
  Edit: 'edit',
} as const

/** One of the {@link InvoiceOpenAction} values. */
export type InvoiceOpenAction = (typeof InvoiceOpenAction)[keyof typeof InvoiceOpenAction]

/** Tabs of the invoice detail screen. */
export enum InvoiceTab {
  Summary = 'summary',
  Payment = 'payment',
  Options = 'options',
  Credit = 'credit',
  Refund = 'refund',
  Notes = 'notes',
}
