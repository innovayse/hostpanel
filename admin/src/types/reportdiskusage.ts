/** Disk and bandwidth use of one hosting account. */
export interface ReportDiskUsageRow {
  /** Client name. */
  clientName: string
  /** Account domain. */
  domain: string
  /** Disk used, formatted. */
  diskUsage: string
  /** Disk limit, formatted. */
  diskLimit: string
  /** Disk used in percent of the limit. */
  diskPercent: number
  /** Bandwidth used, formatted. */
  bwUsage: string
  /** Bandwidth limit, formatted. */
  bwLimit: string
  /** Bandwidth used in percent of the limit. */
  bwPercent: number
}

/** Closed set of reasons a server gave no figures to a refresh; the values are the API's names. */
export const DiskUsageFailureReason = {
  UnsupportedModule: 'UnsupportedModule',
  MissingCredentials: 'MissingCredentials',
  Unreachable: 'Unreachable',
} as const

/** One of the failure reasons. */
export type DiskUsageFailureReason = (typeof DiskUsageFailureReason)[keyof typeof DiskUsageFailureReason]

/** A server the last refresh could not read. */
export interface ReportDiskUsageFailure {
  /** Server name. */
  serverName: string
  /** Why the server gave no figures. */
  reason: DiskUsageFailureReason
}

/** The accounts of one server. */
export interface ReportDiskUsageServer {
  /** Server name. */
  serverName: string
  /** Accounts of the server. */
  rows: ReportDiskUsageRow[]
}

/** The disk usage report. */
export interface ReportDiskUsage {
  /** Servers with their accounts. */
  servers: ReportDiskUsageServer[]
  /** When the figures were last fetched from the servers, if ever. */
  lastUpdated: string | null
  /** Servers the last refresh could not read; empty after a plain read. */
  failures: ReportDiskUsageFailure[]
}
