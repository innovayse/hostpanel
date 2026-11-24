/**
 * Skeleton schema of the client summary screen: three columns of cards (the client, the billing figures, the notes and
 * the linked accounts), the way the screen stacks them, then one card each for the services, the domains and the quotes
 * of the client, every one holding its table.
 *
 * The caption keys of the cards that print figures are named here, so the screen reads its rows from the same lists the
 * skeleton draws and keeps no caption list of its own, and the tables are headed from the same columns the skeleton draws.
 */
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, SkeletonDetailCard, SkeletonTable } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** Closed set of the keys of the cards, the suffixes of their `card-<key>` slots. */
export const SummaryCard = {
  /** Personal and address lines of the client. */
  Info: 'info',
  /** Status, dates and the yes/no flags. */
  Other: 'other',
  /** The contacts of the client. */
  Contacts: 'contacts',
  /** Invoice figures and income. */
  Billing: 'billing',
  /** Counts of services, domains, quotes and tickets. */
  Products: 'products',
  /** The latest emails sent to the client. */
  Emails: 'emails',
  /** The admin notes. */
  Notes: 'notes',
  /** The link to an account in another system. */
  ExternalAccount: 'external',
  /** The VAT number check. */
  Vat: 'vat',
  /** The table of the client's services. */
  Services: 'services',
  /** The table of the client's domains. */
  Domains: 'domains',
  /** The table of the client's current quotes. */
  Quotes: 'quotes',
} as const

/** Locale keys of the captions of the personal and address lines, in display order. */
export const InfoRow = {
  /** First name. */
  FirstName: 'shared.firstName',
  /** Last name. */
  LastName: 'shared.lastName',
  /** Company name. */
  CompanyName: 'shared.companyName',
  /** Email address. */
  Email: 'shared.emailAddress',
  /** Phone number. */
  Phone: 'shared.phoneNumber',
  /** First address line. */
  Address1: 'shared.address1',
  /** Second address line. */
  Address2: 'shared.address2',
  /** City. */
  City: 'shared.city',
  /** State or region. */
  State: 'clients.clientSummary.stateRegion',
  /** Postcode. */
  Postcode: 'shared.postcode',
  /** Country. */
  Country: 'shared.country',
} as const

/** Locale keys of the captions of the status, dates and flags, in display order. */
export const OtherRow = {
  /** Client status. */
  Status: 'shared.status',
  /** Sign-up date. */
  SignupDate: 'clients.clientSummary.signupDate',
  /** How long the client has been with the host. */
  ClientFor: 'clients.clientSummary.clientFor',
  /** Tax exempt flag. */
  TaxExempt: 'shared.taxExempt',
  /** Automatic card processing flag. */
  AutoCcProcessing: 'clients.clientSummary.autoCcProcessing',
  /** Overdue reminders flag. */
  OverdueReminders: 'clients.clientSummary.overdueReminders',
  /** Two-factor flag. */
  TwoFactor: 'clients.clientSummary.twoFactorAuth',
} as const

/** Locale keys of the captions of the invoice status lines, in display order. */
export const InvoiceRow = {
  /** Paid invoices. */
  Paid: 'clients.clientSummary.paid',
  /** Draft invoices. */
  Draft: 'clients.clientSummary.draft',
  /** Unpaid invoices. */
  Unpaid: 'clients.clientSummary.unpaidDue',
  /** Overdue invoices. */
  Overdue: 'clients.clientSummary.overdue',
  /** Cancelled invoices. */
  Cancelled: 'clients.clientSummary.cancelled',
  /** Partially refunded invoices. */
  PartiallyRefunded: 'clients.clientSummary.partiallyRefunded',
  /** Refunded invoices. */
  Refunded: 'clients.clientSummary.refunded',
} as const

/** One caption key of the invoice status lines. */
export type InvoiceRow = (typeof InvoiceRow)[keyof typeof InvoiceRow]

/** Locale keys of the captions of the income lines, in display order. */
export const IncomeRow = {
  /** Gross revenue. */
  GrossRevenue: 'clients.clientSummary.grossRevenue',
  /** Expenses of the client. */
  ClientExpenses: 'clients.clientSummary.clientExpenses',
  /** Net income. */
  NetIncome: 'clients.clientSummary.netIncome',
  /** Credit balance. */
  CreditBalance: 'clients.clientSummary.creditBalance',
} as const

/** One caption key of the income lines. */
export type IncomeRow = (typeof IncomeRow)[keyof typeof IncomeRow]

/** Locale keys of the captions of the record count lines, in display order. */
export const CountRow = {
  /** Active over total services. */
  ActiveServices: 'clients.clientSummary.activeServices',
  /** Total domains. */
  Domains: 'shared.domains',
  /** Accepted over total quotes. */
  AcceptedQuotes: 'clients.clientSummary.acceptedQuotes',
  /** Open over total tickets. */
  OpenTickets: 'clients.clientSummary.openTickets',
} as const

/** One caption key of the record count lines. */
export type CountRow = (typeof CountRow)[keyof typeof CountRow]

/**
 * The caption keys of one of the row sets above, in display order.
 *
 * @param rows - A map of row names to caption keys.
 * @returns The caption keys.
 */
const captionsOf = (rows: Readonly<Record<string, string>>): string[] => Object.values(rows)

/** The cards of the first column: the client, the billing figures and the notes. */
const FIRST_COLUMN: readonly SkeletonDetailCard[] = [
  { key: SummaryCard.Info, title: 'clients.clientSummary.clientInformation', rows: captionsOf(InfoRow) },
  {
    key: SummaryCard.Billing,
    title: 'clients.clientSummary.invoicesBilling',
    rows: [...captionsOf(InvoiceRow), ...captionsOf(IncomeRow)],
  },
  { key: SummaryCard.Notes, title: 'shared.adminNotes' },
]

/** The cards of the second column: the status, the counts and the linked account. */
const SECOND_COLUMN: readonly SkeletonDetailCard[] = [
  { key: SummaryCard.Other, title: 'clients.clientSummary.otherInformation', rows: captionsOf(OtherRow) },
  { key: SummaryCard.Products, title: 'clients.clientSummary.productsServices', rows: captionsOf(CountRow) },
  {
    key: SummaryCard.ExternalAccount,
    title: 'clients.externalAccount.title',
    rows: ['clients.externalAccount.provider', 'clients.externalAccount.accountId'],
  },
]

/** The cards of the third column: the contacts, the latest emails and the VAT check. */
const THIRD_COLUMN: readonly SkeletonDetailCard[] = [
  { key: SummaryCard.Contacts, title: 'clients.clientSummary.contacts' },
  { key: SummaryCard.Emails, title: 'clients.clientSummary.recentEmails' },
  { key: SummaryCard.Vat, title: 'clients.vat.title' },
]

/** The services table: id, product, domain, price, cycle, next due date, status and the row actions. */
export const SUMMARY_SERVICES_TABLE: SkeletonTable = {
  rows: 3,
  columns: [
    { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
    { key: 'product', label: 'shared.productService', size: UiColumnSize.Grow },
    { key: 'domain', label: 'shared.domain' },
    { key: 'price', label: 'shared.price', numeric: true },
    { key: 'billingCycle', label: 'shared.billingCycle' },
    { key: 'nextDue', label: 'clients.clientSummary.nextDue', kind: UiColumnKind.Date },
    { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The domains table: id, name, registrar, status, expiry date, the auto renew flag and the row actions. */
export const SUMMARY_DOMAINS_TABLE: SkeletonTable = {
  rows: 3,
  columns: [
    { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
    { key: 'name', label: 'shared.domainName', size: UiColumnSize.Grow },
    { key: 'registrar', label: 'shared.registrar' },
    { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
    { key: 'expiresAt', label: 'shared.expiryDate', kind: UiColumnKind.Date },
    { key: 'autoRenew', label: 'shared.autoRenew', size: UiColumnSize.Compact },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The quotes table: id, subject, dates, total, stage and the row actions. */
export const SUMMARY_QUOTES_TABLE: SkeletonTable = {
  rows: 3,
  columns: [
    { key: 'id', label: 'shared.id', kind: UiColumnKind.Id },
    { key: 'subject', label: 'shared.subject', size: UiColumnSize.Grow },
    { key: 'dateCreated', label: 'clients.clientSummary.createDate', kind: UiColumnKind.Date },
    { key: 'validUntil', label: 'shared.validUntil', kind: UiColumnKind.Date },
    { key: 'total', label: 'shared.total', numeric: true },
    { key: 'stage', label: 'shared.stage', kind: UiColumnKind.Status },
    { key: 'actions', label: 'shared.actions', actions: true },
  ],
}

/** The three columns of information cards. */
export const SUMMARY_CARDS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [FIRST_COLUMN, SECOND_COLUMN, THIRD_COLUMN],
}

/** The card of the services table. */
export const SUMMARY_SERVICES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: SummaryCard.Services, title: 'clients.clientSummary.productsServices2', table: SUMMARY_SERVICES_TABLE }]],
}

/** The card of the domains table. */
export const SUMMARY_DOMAINS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: SummaryCard.Domains, title: 'shared.domains', table: SUMMARY_DOMAINS_TABLE }]],
}

/** The card of the quotes table. */
export const SUMMARY_QUOTES_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: SummaryCard.Quotes, title: 'clients.clientSummary.currentQuotes', table: SUMMARY_QUOTES_TABLE }]],
}

/** What the client summary screen is made of. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  header: { title: 'clients.clientSummary.summary' },
  parts: [SUMMARY_CARDS_PART, SUMMARY_SERVICES_PART, SUMMARY_DOMAINS_PART, SUMMARY_QUOTES_PART],
}

export default schema
