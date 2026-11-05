/**
 * Types of the upcoming renewals screen: the window staff look ahead over, a service renewing inside it
 * and a domain renewal line left claimed after an unknown registrar answer.
 */
import type { BillingCycle } from '@/types/service'

/** How far ahead the screen looks; the values are the names the API takes. */
export enum RenewalWindow {
  /** The next 30 days. */
  Next30Days = 'Next30Days',
  /** The next 60 days. */
  Next60Days = 'Next60Days',
  /** The next 90 days. */
  Next90Days = 'Next90Days',
}

/** One active service renewing inside the window. */
export interface UpcomingServiceRenewal {
  /** Service id. */
  id: number
  /** Owning client id. */
  clientId: number
  /** Full name of the owning client. */
  clientName: string
  /** Name of the ordered product. */
  productName: string
  /** Domain the service runs on, or null. */
  domain: string | null
  /** Recurring price of the period to bill. */
  amount: number
  /** ISO 4217 code of the client billing currency. */
  priceCurrency: string
  /** Billing cycle. */
  billingCycle: BillingCycle
  /** ISO 8601 start of the next period to bill. */
  nextRenewalAt: string
  /** Whether the billing runs look after the service; false means staff bill it by hand. */
  autoRenew: boolean
}

/** A paid domain renewal line left claimed and unapplied, waiting for staff to check the registrar. */
export interface ClaimedDomainRenewal {
  /** The invoice the line is on. */
  invoiceId: number
  /** The line id. */
  itemId: number
  /** The client the invoice belongs to. */
  clientId: number
  /** The domain the line renews. */
  registeredDomainId: number
  /** Line text, which names the domain and the term. */
  description: string
  /** ISO 8601 start of the period the line pays for. */
  periodStart: string
  /** ISO 8601 end of the period the line pays for. */
  periodEnd: string
  /** ISO 8601 time the renewal was claimed, just before the registrar was asked. */
  claimedAt: string
}
