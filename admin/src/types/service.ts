/**
 * A provisioned service in its three depths, plus a pending cancellation against one.
 */

import type { Domain } from './domain'
import type { Product } from './product'

/** Represents a hosted service (hosting, VPS, etc.). */
export interface Service {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Product name (e.g. Shared Hosting, VPS). */
  productName: string
  /** Primary domain assigned to the service. */
  domain: string
  /** Service status (active, suspended, cancelled, pending). */
  status: string
  /** ISO 8601 next due date for renewal. */
  nextDueDate: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Enriched service DTO for admin service list views. */
export interface ServiceListItem {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Product display name. */
  productName: string
  /** Linked domain name, or null if no domain is linked. */
  domain: string | null
  /** Resolved price based on billing cycle. */
  price: number
  /** ISO 4217 currency code (e.g. "USD"). */
  priceCurrency: string
  /** Billing cycle: "monthly" or "annual". */
  billingCycle: string
  /** Current lifecycle status (Active, Pending, Suspended, Terminated). */
  status: string
  /** ISO 8601 next renewal date, or null if not yet active. */
  nextDueDate: string | null
}

/** Full service detail returned by GET /api/services/{id}. */
export interface ServiceDetail {
  /** Unique service identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Product identifier. */
  productId: number
  /** Product display name. */
  productName: string
  /** Linked domain name, if any. */
  domain?: string
  /** Dedicated IP address, if assigned. */
  dedicatedIp?: string
  /** Control panel or service username. */
  username?: string
  /** Control panel or service password. */
  password?: string
  /** Quantity of units purchased. */
  quantity: number
  /** Amount charged for the first payment. */
  firstPaymentAmount: number
  /** Recurring charge per billing cycle. */
  recurringAmount: number
  /** Payment method identifier or label. */
  paymentMethod?: string
  /** Applied promotion code, if any. */
  promotionCode?: string
  /** External subscription ID (e.g. Stripe subscription). */
  subscriptionId?: string
  /** Billing cycle: "monthly" or "annual". */
  billingCycle: string
  /** Current lifecycle status (Active, Pending, Suspended, Terminated). */
  status: string
  /** External provisioning reference (e.g. cPanel account ID). */
  provisioningRef?: string
  /** ISO 8601 next renewal date, if applicable. */
  nextDueDate?: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** ISO 8601 termination timestamp, if terminated. */
  terminatedAt?: string
  /** Whether automatic suspension is overridden. */
  overrideAutoSuspend: boolean
  /** ISO 8601 date until which the service is suspended. */
  suspendUntil?: string
  /** Whether the service auto-terminates at end of billing cycle. */
  autoTerminateEndOfCycle: boolean
  /** Reason for auto-termination, if applicable. */
  autoTerminateReason?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** FK to the assigned server, if any. */
  serverId?: number
  /** Display name of the assigned server. */
  serverName?: string
}

/** DTO for a cancellation request in the admin list. */
export interface CancellationRequestItem {
  /** Unique identifier. */
  id: number
  /** ID of the service being cancelled. */
  serviceId: number
  /** Name of the product/service. */
  serviceName: string
  /** ID of the owning client. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /**
   * Cancellation type as the backend enum member name -- `"Immediate"` or
   * `"EndOfBillingPeriod"`. Not display text; the screen owns the wording.
   */
  type: string
  /** Reason provided by the client. */
  reason: string | null
  /** Request status ("Open" or "Closed"). */
  status: string
  /** ISO 8601 creation date. */
  createdAt: string
}
