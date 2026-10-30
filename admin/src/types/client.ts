/**
 * A client account: the list row, the full record, and the two rollups its screens show.
 */

import type { Contact } from '@/types/contact'
import type { ContactNotifications } from '@/types/contactnotifications'
import type { EmailLog } from '@/types/emaillog'
import type { InvoiceStatusCount } from '@/types/invoice'
import type { PostalAddress } from '@/types/postaladdress'

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
  /** Account status. */
  status: ClientStatus
  /** True if the linked Identity user has been deleted. */
  isUserDeleted: boolean
  /** True if the user has TOTP 2FA enabled. */
  twoFactorEnabled: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Language, billing currency and payment preferences of a client, with the internal notes. */
export interface ClientPreferences {
  /** Preferred UI language of the account holder, or null where the deployment holds none. */
  language?: string | null
  /** ISO 4217 currency code. */
  currency?: string | null
  /** Payment method identifier or label. */
  paymentMethod?: string | null
  /** Billing contact name or reference. */
  billingContact?: string | null
  /** Internal admin notes. */
  adminNotes?: string | null
}

/** The account switches staff set on a client. */
export interface ClientSettings {
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
}

/** Full client DTO returned by the detail endpoint. */
export interface ClientDetail extends ClientListItem {
  /** Phone number. */
  phone?: string
  /** Billing address. */
  address: PostalAddress
  /** Language, currency, payment preferences and internal notes. */
  preferences: ClientPreferences
  /** Which kinds of email the client receives. */
  notifications: ContactNotifications
  /** The account switches staff set on the client. */
  settings: ClientSettings
  /** True if the user has TOTP 2FA enabled. */
  twoFactorEnabled: boolean
  /** Associated contacts. */
  contacts: Contact[]
}

/** Invoice count and total per status for one client. */
export interface ClientInvoiceStatuses {
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
  /** Stats of the paid invoices of which part has been refunded. */
  partiallyRefunded: InvoiceStatusCount
}

/** What a client's transactions add up to. */
export interface ClientTransactionTotals {
  /** Total income from transactions. */
  grossRevenue: number
  /** Total outgoing from transactions. */
  clientExpenses: number
  /** Total transaction fees. */
  totalFees: number
  /** Computed: grossRevenue - clientExpenses - totalFees. */
  netIncome: number
}

/** How many services, domains, quotes and tickets a client has. */
export interface ClientRecordCounts {
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
}

/** Aggregated summary data for the client profile dashboard. */
export interface ClientSummaryData {
  /** Invoice count and total per status. */
  invoices: ClientInvoiceStatuses
  /** Income, outgoing and fee totals from transactions. */
  transactions: ClientTransactionTotals
  /** Current credit balance. */
  creditBalance: number
  /** Number of services, domains, quotes and tickets. */
  counts: ClientRecordCounts
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

/** Filter parameters for the clients list endpoint. */
export interface ClientFilters {
  /** Name/company search term. */
  search?: string
  /** Email partial match. */
  email?: string
  /** Phone partial match. */
  phone?: string
  /** Status filter. */
  status?: ClientStatus
}

/** Currency row of the admin currency configuration, as the client pickers read it. */
export interface ConfiguredCurrency {
  /** ISO 4217 code. */
  code: string
  /** Text shown before an amount. */
  prefix: string
  /** Text shown after an amount. */
  suffix: string
  /** Whether the operator enabled the currency. */
  isEnabled: boolean
}

/** Status of a client account, exactly as the API sends it. */
export enum ClientStatus {
  Active = 'Active',
  Inactive = 'Inactive',
  Suspended = 'Suspended',
  Closed = 'Closed',
}
