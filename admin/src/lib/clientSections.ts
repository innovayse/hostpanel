/**
 * The sections of the client detail pages, as data: which tab opens which route segment, under which caption.
 *
 * One list serves the section tab strip and the skeleton schema of the client layout, so the strip a skeleton
 * draws while the layout downloads carries exactly the captions of the real one. Pure data: captions are locale
 * keys, resolved by whoever draws them.
 */
import { ClientSection } from '@/lib/routes'

/** Keys of the sections; also the suffix of each tab's `data-testid`. */
export const ClientNavKey = {
  Summary: 'summary',
  Profile: 'profile',
  Services: 'services',
  Domains: 'domains',
  Users: 'users',
  Contacts: 'contacts',
  Billable: 'billable',
  Invoices: 'invoices',
  Quotes: 'quotes',
  Transactions: 'transactions',
  Credit: 'credit',
  Tickets: 'tickets',
  Emails: 'emails',
  Log: 'log',
} as const

/** One of the section keys. */
export type ClientNavKey = (typeof ClientNavKey)[keyof typeof ClientNavKey]

/** One section of the client: its key, the route segment it opens and the locale key of its caption. */
export interface ClientNavEntry {
  /** Key of the section; also the tab value. */
  key: ClientNavKey
  /** The route segment under `/clients/:id/` the tab opens. */
  section: ClientSection
  /** Locale key of the caption. */
  label: string
}

/** The sections in tab order. */
export const CLIENT_NAV: readonly ClientNavEntry[] = [
  { key: ClientNavKey.Summary, section: ClientSection.Summary, label: 'clients.clientSummary.summary' },
  { key: ClientNavKey.Profile, section: ClientSection.Profile, label: 'clients.contactForm.profile' },
  { key: ClientNavKey.Services, section: ClientSection.Services, label: 'clients.clientSummary.productsServices2' },
  { key: ClientNavKey.Domains, section: ClientSection.Domains, label: 'shared.domains' },
  { key: ClientNavKey.Users, section: ClientSection.Users, label: 'clients.clientInnerSidebar.users' },
  { key: ClientNavKey.Contacts, section: ClientSection.Contacts, label: 'clients.clientSummary.contacts' },
  { key: ClientNavKey.Billable, section: ClientSection.BillableItems, label: 'nav.billingChildren.billableItems' },
  { key: ClientNavKey.Invoices, section: ClientSection.Invoices, label: 'shared.invoices' },
  { key: ClientNavKey.Quotes, section: ClientSection.Quotes, label: 'shared.quotes' },
  { key: ClientNavKey.Transactions, section: ClientSection.Transactions, label: 'shared.transactions' },
  { key: ClientNavKey.Credit, section: ClientSection.Credit, label: 'clients.clientCredit.credit' },
  { key: ClientNavKey.Tickets, section: ClientSection.Tickets, label: 'clients.clientTickets.tickets' },
  { key: ClientNavKey.Emails, section: ClientSection.Emails, label: 'clients.clientEmails.emails' },
  { key: ClientNavKey.Log, section: ClientSection.Log, label: 'clients.clientInnerSidebar.log' },
]
