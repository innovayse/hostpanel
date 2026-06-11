export interface MigrationEntityProgress {
  imported: number
  total: number
  done: boolean
}

export interface MigrationProgress {
  clients: MigrationEntityProgress
  invoices: MigrationEntityProgress
  services: MigrationEntityProgress
  domains: MigrationEntityProgress
  tickets: MigrationEntityProgress
  products: MigrationEntityProgress
  orders: MigrationEntityProgress
  transactions: MigrationEntityProgress
  quotes: MigrationEntityProgress
  knowledgebase: MigrationEntityProgress
  contacts: MigrationEntityProgress
  ticketReplies: MigrationEntityProgress
}

export interface MigrationEntitySelection {
  clients: boolean
  invoices: boolean
  services: boolean
  domains: boolean
  tickets: boolean
  products: boolean
  orders: boolean
  transactions: boolean
  quotes: boolean
  knowledgebase: boolean
  contacts: boolean
  ticketReplies: boolean
}

export interface MigrationLogEntry {
  id: number
  entityType: 'Clients' | 'Invoices' | 'Services' | 'Domains' | 'Tickets' | 'Products' | 'Orders' | 'Transactions' | 'Quotes' | 'Knowledgebase' | 'Contacts' | 'TicketReplies'
  identifier: string
  action: 'Imported' | 'Skipped' | 'Failed'
  reason: string | null
  createdAt: string
}

export interface MigrationLogPage {
  items: MigrationLogEntry[]
  totalCount: number
  page: number
  pageSize: number
}

export interface MigrationJob {
  id: number
  key: string
  sourceUrl: string
  status: 'Pending' | 'InProgress' | 'Completed' | 'Failed'
  label: string | null
  errorMessage: string | null
  entitySelection: MigrationEntitySelection
  progress: MigrationProgress
  overallPercent: number
  pluginConnected: boolean
  lastPingAt: string | null
  createdAt: string
  completedAt: string | null
}
