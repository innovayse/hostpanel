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
}

export interface MigrationEntitySelection {
  clients: boolean
  invoices: boolean
  services: boolean
  domains: boolean
  tickets: boolean
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
