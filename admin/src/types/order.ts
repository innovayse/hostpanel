/**
 * An order, its detail and one line on it.
 */

import type { Domain } from './domain'
import type { Invoice } from './invoice'
import type { Product } from './product'

/** DTO for order list items in admin view. */
export interface OrderListItem {
  /** Unique order identifier. */
  id: number
  /** Human-readable order number (e.g. "ORD-0001"). */
  orderNumber: string
  /** FK to the owning client. */
  clientId: number
  /** Client display name. */
  clientName: string
  /** Current order status. */
  status: 'Pending' | 'Active' | 'Cancelled' | 'Fraud'
  /** Payment gateway module name. */
  paymentMethod: string
  /** Total order amount. */
  total: number
  /** Linked invoice ID, if any. */
  invoiceId: number | null
  /** Number of items in the order. */
  itemCount: number
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** DTO for full order details in admin view. */
export interface OrderDetail extends OrderListItem {
  /** Invoice status if invoice exists. */
  invoiceStatus: string | null
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
  billingCycle: string
  /** Domain name if applicable. */
  domain: string | null
  /** Hostname if applicable. */
  hostname: string | null
  /** First payment amount. */
  firstPaymentAmount: number
  /** Recurring amount. */
  recurringAmount: number
  /** Item status. */
  status: 'Pending' | 'Active' | 'Cancelled'
}
