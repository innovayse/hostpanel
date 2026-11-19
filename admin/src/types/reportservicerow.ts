/**
 * The row shape of the services report.
 */

import type { BillingCycle, ServiceStatus } from '@/types/service'

/** One row of the services report. */
export interface ReportServiceRow {
  /** Service id. */
  id: number
  /** Client id. */
  clientId: number
  /** Client name. */
  clientName: string
  /** Product id. */
  productId: number
  /** Product name; every service has one, since its product cannot be deleted. */
  productName: string
  /** Domain of the service, when it has one. */
  domain: string | null
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** Amount of the first payment. */
  firstPaymentAmount: number
  /** Amount of each renewal. */
  recurringAmount: number
  /** ISO 4217 code of the currency both amounts are in: the client's billing currency. */
  currencyCode: string
  /** Payment method, when set. */
  paymentMethod: string | null
  /** Registration date. */
  registrationDate: string
  /** Next due date, when active. */
  nextDueDate: string | null
  /** Termination date, when terminated. */
  terminatedAt: string | null
  /** Service status. */
  status: ServiceStatus
  /** Staff notes. */
  notes: string | null
}
