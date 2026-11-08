/**
 * An order, its detail and one line on it.
 */

import type { InvoiceStatus } from '@/types/invoice'
import type { BillingCycle } from '@/types/service'

/** DTO for order list items in admin view. */
export interface OrderListItem {
  /** Unique order identifier. */
  id: number
  /** Human-readable order number (e.g. "ORD-0001"). */
  orderNumber: string
  /** FK to the owning client. */
  clientId: number
  /** Client display name; every order has one, since its client cannot be deleted. */
  clientName: string
  /** Current order status. */
  status: OrderStatus
  /** Payment gateway module name. */
  paymentMethod: string
  /** Total order amount. */
  total: number
  /** ISO 4217 code the order's amounts are in. */
  currency: string
  /** Linked invoice ID, if any. */
  invoiceId: number | null
  /** Number of items in the order. */
  itemCount: number
  /** Number of lines staff can retry now: failed lines, plus lines a dead retry left in progress past the lease. */
  failedLineCount: number
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** DTO for full order details in admin view. */
export interface OrderDetail extends OrderListItem {
  /** Invoice status if invoice exists. */
  invoiceStatus: InvoiceStatus | null
  /** Invoice total, tax included and credit taken off, when an invoice exists; `total` is the lines' pre-tax sum. */
  invoiceTotal: number | null
  /** Client IP address at checkout. */
  ipAddress: string | null
  /** Admin notes. */
  notes: string | null
  /** Order line items. */
  items: OrderItemDetail[]
}

/** DTO for an individual order item. */
export interface OrderItemDetail {
  /** Item ID. */
  id: number
  /** Product ID. */
  productId: number
  /** Product name snapshot at order time. */
  productName: string
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** Domain name if applicable. */
  domain: string | null
  /** Hostname if applicable. */
  hostname: string | null
  /** First payment amount. */
  firstPaymentAmount: number
  /** Recurring amount. */
  recurringAmount: number
  /** Item status. */
  status: OrderItemStatus
  /** Where fulfilling the line stands. */
  fulfilmentState: OrderItemFulfilmentState
  /** The code the last failed attempt ended with, null when the line did not fail or gave none. */
  fulfilmentErrorCode: string | null
  /** How many times fulfilling the line has been attempted, the first run included. */
  fulfilmentAttempts: number
  /** ISO 8601 moment the last attempt started or ended, null before the first one. */
  lastFulfilmentAttemptAt: string | null
}

/** What a line of an order is fulfilled as, exactly as the API sends it. */
export enum OrderLineKind {
  Service = 'Service',
  Domain = 'Domain',
}

/** One order line that accepting the order could not fulfil. */
export interface OrderLineFailure {
  /** The order line. */
  itemId: number
  /** Whether the line is a service or a domain line. */
  kind: OrderLineKind
  /** Product name the line was ordered under. */
  productName: string
  /** Domain the line names, if any. */
  domain: string | null
  /** The line's first payment before tax. */
  firstPaymentAmount: number
  /** The refusal code, if the use case gave one. */
  errorCode: string | null
  /** Human-readable reason. */
  reason: string
}

/** What accepting an order left unfulfilled. */
export interface OrderFulfillment {
  /** Lines that could not be fulfilled; empty when every line was. */
  failedLines: OrderLineFailure[]
}

/** Filter and paging of the order list. */
export interface OrderFilter {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
  /** Status to keep; null or empty for all. */
  status?: OrderStatus | null
}

/** Status of an order, exactly as the API sends it. */
export enum OrderStatus {
  Pending = 'Pending',
  Active = 'Active',
  Cancelled = 'Cancelled',
  Fraud = 'Fraud',
  Incomplete = 'Incomplete',
}

/** Status of one line on an order, exactly as the API sends it. */
export enum OrderItemStatus {
  Pending = 'Pending',
  Active = 'Active',
  Cancelled = 'Cancelled',
}

/** Where fulfilling one order line stands, exactly as the API sends it. */
export enum OrderItemFulfilmentState {
  /** No attempt recorded yet. */
  Pending = 'Pending',
  /** A retry is running the line. */
  InProgress = 'InProgress',
  /** The line was fulfilled. */
  Fulfilled = 'Fulfilled',
  /** The last attempt failed; the line can be retried. */
  Failed = 'Failed',
  /** The line failed and its cost was given back; it is closed. */
  Refunded = 'Refunded',
  /** The line failed and its cost is being given back right now; no retry may claim it. */
  Refunding = 'Refunding',
  /** Only part of the cost was given back; the line is closed to retries and staff finish the refund by hand. */
  PartiallyRefunded = 'PartiallyRefunded',
}
