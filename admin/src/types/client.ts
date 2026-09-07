/**
 * A client account: the list row, the full record, and the two rollups its screens show.
 */

import type { Contact } from './contact'
import type { Domain } from './domain'
import type { EmailLog } from './emaillog'
import type { Invoice, InvoiceStatusCount } from './invoice'
import type { Product } from './product'
import type { Ticket } from './ticket'

/** Summary DTO returned by the clients list endpoint. */
export interface ClientListItem {
  /** Unique client identifier. */
  id: number
  /** Identity user ID. */
  userId: string
  /** Email address from the linked Identity user. */
  email: string
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Optional company name. */
  companyName?: string
  /** Account status (Active, Inactive, Suspended, Closed). */
  status: string
  /** True if the linked Identity user has been deleted. */
  isUserDeleted: boolean
  /** True if the user has TOTP 2FA enabled. */
  twoFactorEnabled: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Full client DTO returned by the detail endpoint. */
export interface ClientDetail extends ClientListItem {
  /** Phone number. */
  phone?: string
  /** Street address. */
  street?: string
  /** Second address line. */
  address2?: string
  /** City. */
  city?: string
  /** State or province. */
  state?: string
  /** Postal code. */
  postCode?: string
  /** ISO 3166-1 alpha-2 country code. */
  country?: string
  /** ISO 4217 currency code. */
  currency?: string
  /** Payment method identifier or label. */
  paymentMethod?: string
  /** Billing contact name or reference. */
  billingContact?: string
  /** Internal admin notes. */
  adminNotes?: string
  /** Whether the client receives general notification emails. */
  notifyGeneral: boolean
  /** Whether the client receives invoice notification emails. */
  notifyInvoice: boolean
  /** Whether the client receives support notification emails. */
  notifySupport: boolean
  /** Whether the client receives product notification emails. */
  notifyProduct: boolean
  /** Whether the client receives domain notification emails. */
  notifyDomain: boolean
  /** Whether the client receives affiliate notification emails. */
  notifyAffiliate: boolean
  /** Whether late fees are applied to overdue invoices. */
  lateFees: boolean
  /** Whether overdue notices are sent for this client. */
  overdueNotices: boolean
  /** Whether the client is exempt from taxes. */
  taxExempt: boolean
  /** Whether invoices are generated separately per product. */
  separateInvoices: boolean
  /** Whether credit card processing is disabled for this client. */
  disableCcProcessing: boolean
  /** Whether the client has opted in to marketing emails. */
  marketingOptIn: boolean
  /** Whether the client status updates are tracked. */
  statusUpdate: boolean
  /** Whether the client may use single sign-on. */
  allowSso: boolean
  /** True if the user has TOTP 2FA enabled. */
  twoFactorEnabled: boolean
  /** Associated contacts. */
  contacts: Contact[]
}

/** Aggregated summary data for the client profile dashboard. */
export interface ClientSummaryData {
  /** Draft invoice stats. */
  draft: InvoiceStatusCount
  /** Unpaid invoice stats. */
  unpaid: InvoiceStatusCount
  /** Paid invoice stats. */
  paid: InvoiceStatusCount
  /** Overdue invoice stats. */
  overdue: InvoiceStatusCount
  /** Cancelled invoice stats. */
  cancelled: InvoiceStatusCount
  /** Refunded invoice stats. */
  refunded: InvoiceStatusCount
  /** Total income from transactions. */
  grossRevenue: number
  /** Total outgoing from transactions. */
  clientExpenses: number
  /** Total transaction fees. */
  totalFees: number
  /** Computed: grossRevenue - clientExpenses - totalFees. */
  netIncome: number
  /** Current credit balance. */
  creditBalance: number
  /** Services with Active status. */
  activeServicesCount: number
  /** Total number of services. */
  totalServicesCount: number
  /** Total number of domains. */
  totalDomainsCount: number
  /** Quotes with Accepted stage. */
  acceptedQuotesCount: number
  /** Total number of quotes. */
  totalQuotesCount: number
  /** Open tickets. */
  openTicketsCount: number
  /** Total tickets. */
  totalTicketsCount: number
  /** Last 5 email log entries. */
  recentEmails: EmailLog[]
}

/** Ticket statistics for a client, broken down by time period. */
export interface ClientTicketStats {
  /** Tickets opened in the current calendar month. */
  openedThisMonth: number
  /** Tickets opened in the previous calendar month. */
  openedLastMonth: number
  /** Tickets opened in the current calendar year. */
  openedThisYear: number
  /** Tickets opened in the previous calendar year. */
  openedLastYear: number
}
