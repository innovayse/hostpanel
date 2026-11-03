/**
 * Shapes of the migration endpoints of the admin API: jobs, their progress, their import log and
 * the options that start one.
 */

/** Import counters of one entity group. */
export interface MigrationEntityProgress {
  /** Rows written. */
  imported: number
  /** Rows skipped because they already existed. */
  skipped: number
  /** Rows whose import failed with an error. */
  failed: number
  /** Rows the source reported for this group. */
  total: number
  /** True once the group has been fully processed. */
  done: boolean
}

/** Progress of every entity group of a job. */
export interface MigrationProgress {
  /** Clients. */
  clients: MigrationEntityProgress
  /** Invoices. */
  invoices: MigrationEntityProgress
  /** Services. */
  services: MigrationEntityProgress
  /** Domains. */
  domains: MigrationEntityProgress
  /** Tickets. */
  tickets: MigrationEntityProgress
  /** Products. */
  products: MigrationEntityProgress
  /** Orders. */
  orders: MigrationEntityProgress
  /** Transactions. */
  transactions: MigrationEntityProgress
  /** Quotes. */
  quotes: MigrationEntityProgress
  /** Knowledgebase articles. */
  knowledgebase: MigrationEntityProgress
  /** Contacts. */
  contacts: MigrationEntityProgress
  /** Ticket replies. */
  ticketReplies: MigrationEntityProgress
  /** Announcements. */
  announcements: MigrationEntityProgress
  /** Downloads. */
  downloads: MigrationEntityProgress
  /** Network issues. */
  networkIssues: MigrationEntityProgress
}

/** Which entity groups a job exports. */
export interface MigrationEntitySelection {
  /** Export clients. */
  clients: boolean
  /** Export invoices. */
  invoices: boolean
  /** Export services. */
  services: boolean
  /** Export domains. */
  domains: boolean
  /** Export tickets. */
  tickets: boolean
  /** Export products. */
  products: boolean
  /** Export orders. */
  orders: boolean
  /** Export transactions. */
  transactions: boolean
  /** Export quotes. */
  quotes: boolean
  /** Export knowledgebase articles. */
  knowledgebase: boolean
  /** Export contacts. */
  contacts: boolean
  /** Export ticket replies. */
  ticketReplies: boolean
  /** Export announcements. */
  announcements: boolean
  /** Export downloads. */
  downloads: boolean
  /** Export network issues. */
  networkIssues: boolean
}

/** One line of a job's import log. */
export interface MigrationLogEntry {
  /** Log row id. */
  id: number
  /** The entity group the row belongs to. */
  entityType: MigrationEntityType
  /** Identifier of the source row. */
  identifier: string
  /** What happened to the row. */
  action: MigrationLogAction
  /** Why the row was skipped, null unless it was skipped. */
  skipReason: MigrationSkipReason | null
  /** Technical detail of the error for a failed row, null otherwise. */
  reason: string | null
  /** When the row was logged. */
  createdAt: string
}

/** A migration job as the API returns it. */
export interface MigrationJob {
  /** Job id. */
  id: number
  /** Public key the source plugin authenticates with. */
  key: string
  /** Address of the source system. */
  sourceUrl: string
  /** Lifecycle state. */
  status: MigrationStatus
  /** Label the operator gave the job, null when none. */
  label: string | null
  /** Why the job failed, null otherwise; the detail stays in the server log. */
  failure: MigrationJobFailure | null
  /** Which entity groups the job exports. */
  entitySelection: MigrationEntitySelection
  /** Per-group counters. */
  progress: MigrationProgress
  /** Overall completion, 0 to 100. */
  overallPercent: number
  /** True when the source plugin has connected. */
  pluginConnected: boolean
  /** Last time the source plugin pinged, null when never. */
  lastPingAt: string | null
  /** When the job was created. */
  createdAt: string
  /** When the job finished, null while running. */
  completedAt: string | null
}

/** Options that create a migration job; every export flag defaults to true. */
export interface CreateMigrationJobOptions {
  /** Label for the job. */
  label?: string
  /** Address of the source system. */
  sourceUrl: string
  /** Export clients. */
  exportClients?: boolean
  /** Export invoices. */
  exportInvoices?: boolean
  /** Export services. */
  exportServices?: boolean
  /** Export domains. */
  exportDomains?: boolean
  /** Export tickets. */
  exportTickets?: boolean
  /** Export products. */
  exportProducts?: boolean
  /** Export orders. */
  exportOrders?: boolean
  /** Export transactions. */
  exportTransactions?: boolean
  /** Export quotes. */
  exportQuotes?: boolean
  /** Export knowledgebase articles. */
  exportKnowledgebase?: boolean
  /** Export contacts. */
  exportContacts?: boolean
  /** Export ticket replies. */
  exportTicketReplies?: boolean
  /** Export announcements. */
  exportAnnouncements?: boolean
  /** Export downloads. */
  exportDownloads?: boolean
  /** Export network issues. */
  exportNetworkIssues?: boolean
}

/** Body of `POST /admin/migrations`: the `CreateMigrationJobCommand` as the API binds it. */
export interface CreateMigrationJobPayload {
  /** Label for the job, null when none. */
  label: string | null
  /** Address of the source system. */
  sourceUrl: string
  /** Which entity groups the job exports. */
  selection: MigrationEntitySelection
}

/** Filter and paging of a job's import log. */
export interface MigrationLogQuery {
  /** Keep only rows with this action. */
  action?: MigrationLogAction
  /** Keep only rows of this entity group. */
  entityType?: MigrationEntityType
  /** One-based page number. */
  page: number
  /** Rows per page. */
  pageSize: number
}

/** Closed set of lifecycle states of a migration job. */
export const MigrationStatus = {
  /** Created; waiting for the source plugin and for the import to start. */
  Pending: 'Pending',
  /** The import is running. */
  InProgress: 'InProgress',
  /** The import finished. */
  Completed: 'Completed',
  /** The import stopped with an error. */
  Failed: 'Failed',
  /** The import was requested and waits for the background worker. */
  Queued: 'Queued',
} as const

/** Closed set of outcomes the import log records for a row. */
export const MigrationLogAction = {
  /** The row was written. */
  Imported: 'Imported',
  /** The row already existed and was skipped. */
  Skipped: 'Skipped',
  /** The row could not be written. */
  Failed: 'Failed',
} as const

/** Keys of the entity groups a job can export, in display order. */
export const MIGRATION_ENTITY_KEYS = [
  'clients', 'invoices', 'services', 'domains', 'tickets', 'products', 'orders', 'transactions',
  'quotes', 'knowledgebase', 'contacts', 'ticketReplies', 'announcements', 'downloads', 'networkIssues',
] as const

/** One entity group key. */
export type MigrationEntityKey = (typeof MIGRATION_ENTITY_KEYS)[number]

/** The export flags of a job, as a form holds them before the label and address are added. */
export interface MigrationExportFlags {
  /** Export clients. */
  exportClients: boolean
  /** Export invoices. */
  exportInvoices: boolean
  /** Export services. */
  exportServices: boolean
  /** Export domains. */
  exportDomains: boolean
  /** Export tickets. */
  exportTickets: boolean
  /** Export products. */
  exportProducts: boolean
  /** Export orders. */
  exportOrders: boolean
  /** Export transactions. */
  exportTransactions: boolean
  /** Export quotes. */
  exportQuotes: boolean
  /** Export knowledgebase articles. */
  exportKnowledgebase: boolean
  /** Export contacts. */
  exportContacts: boolean
  /** Export ticket replies. */
  exportTicketReplies: boolean
  /** Export announcements. */
  exportAnnouncements: boolean
  /** Export downloads. */
  exportDownloads: boolean
  /** Export network issues. */
  exportNetworkIssues: boolean
}

/** One of the {@link MigrationStatus} values. */
export type MigrationStatus = (typeof MigrationStatus)[keyof typeof MigrationStatus]

/** Closed set of reasons a job failed, as the API sends them. */
export const MigrationJobFailure = {
  /** A reason not listed here, including jobs that failed before the reason was recorded. */
  Unknown: 'Unknown',
  /** The source plugin could not be reached. */
  SourceUnreachable: 'SourceUnreachable',
  /** The source plugin answered with a refusal, for example a wrong key. */
  SourceRefused: 'SourceRefused',
  /** The records could not be read or saved here. */
  ImportFailed: 'ImportFailed',
  /** The import was stopped before it finished. */
  Cancelled: 'Cancelled',
  /** The server stopped while the import was queued or running. */
  Interrupted: 'Interrupted',
  /** The source address is not one the server may call. */
  SourceAddressNotAllowed: 'SourceAddressNotAllowed',
} as const

/** One of the {@link MigrationJobFailure} values. */
export type MigrationJobFailure = (typeof MigrationJobFailure)[keyof typeof MigrationJobFailure]

/** One of the {@link MigrationLogAction} values. */
export type MigrationLogAction = (typeof MigrationLogAction)[keyof typeof MigrationLogAction]

/** Closed set of reasons the import log gives for a skipped or failed row, as the API sends them. */
export const MigrationSkipReason = {
  /** The same record already exists here. */
  AlreadyExists: 'AlreadyExists',
  /** The client the record belongs to does not exist here. */
  ClientNotFound: 'ClientNotFound',
  /** The account for a client could not be created. */
  AccountNotProvisioned: 'AccountNotProvisioned',
  /** The product group the record refers to was not imported. */
  ProductGroupNotFound: 'ProductGroupNotFound',
  /** No product exists here to attach the service to. */
  NoProducts: 'NoProducts',
  /** The ticket the reply belongs to does not exist here. */
  TicketNotFound: 'TicketNotFound',
  /** The ticket the reply belongs to is closed. */
  TicketClosed: 'TicketClosed',
  /** Importing the record failed with an error; the detail stays in the server log. */
  ImportFailed: 'ImportFailed',
  /** No product here carries the name the record refers to. */
  ProductNotFound: 'ProductNotFound',
  /** The invoice the record pays was not imported here. */
  InvoiceNotFound: 'InvoiceNotFound',
  /** The source record carries no date the import needs. */
  DateMissing: 'DateMissing',
  /** No department exists here to place the ticket in. */
  DepartmentNotFound: 'DepartmentNotFound',
  /** No download category exists here to place the file in. */
  DownloadCategoryNotFound: 'DownloadCategoryNotFound',
  /** The client record has no first name and no email address to take one from. */
  NameMissing: 'NameMissing',
  /** The record names a billing cycle that cannot be billed here. */
  BillingCycleUnsupported: 'BillingCycleUnsupported',
  /** Every line of the order is a domain or a service addon, which has no catalogue product here. */
  NoProductLines: 'NoProductLines',
  /** The record carries a currency that is not a three-letter code. */
  CurrencyInvalid: 'CurrencyInvalid',
} as const

/** One of the {@link MigrationSkipReason} values. */
export type MigrationSkipReason = (typeof MigrationSkipReason)[keyof typeof MigrationSkipReason]

/** Closed set of entity groups the import log names a row under, as the API sends them. */
export const MigrationEntityType = {
  /** Clients. */
  Clients: 'Clients',
  /** Invoices. */
  Invoices: 'Invoices',
  /** Services. */
  Services: 'Services',
  /** Domains. */
  Domains: 'Domains',
  /** Tickets. */
  Tickets: 'Tickets',
  /** Products. */
  Products: 'Products',
  /** Orders. */
  Orders: 'Orders',
  /** Transactions. */
  Transactions: 'Transactions',
  /** Quotes. */
  Quotes: 'Quotes',
  /** Knowledgebase articles. */
  Knowledgebase: 'Knowledgebase',
  /** Contacts. */
  Contacts: 'Contacts',
  /** Ticket replies. */
  TicketReplies: 'TicketReplies',
  /** Announcements. */
  Announcements: 'Announcements',
  /** Downloads. */
  Downloads: 'Downloads',
  /** Network issues. */
  NetworkIssues: 'NetworkIssues',
} as const

/** One of the {@link MigrationEntityType} values. */
export type MigrationEntityType = (typeof MigrationEntityType)[keyof typeof MigrationEntityType]
