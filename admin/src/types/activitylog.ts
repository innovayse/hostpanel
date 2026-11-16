/** Represents a single admin activity log entry for a client. */
export interface ActivityLog {
  /** Unique identifier. */
  id: number
  /** Human-readable description of the action performed. */
  description: string
  /** Display name of the admin who performed the action. */
  adminName: string | null
  /** Email of the admin who performed the action. */
  adminEmail: string | null
  /** IP address from which the action originated. */
  ipAddress: string | null
  /** UTC ISO timestamp of the action. */
  createdAt: string
}

/** What narrows the client activity log: the search text and the period; each member left out means no narrowing. */
export interface ActivityLogFilters {
  /** Search term over the admin name, the admin email, the description and the IP address; empty for none. */
  search?: string
  /** First day kept, `YYYY-MM-DD`. */
  from?: string
  /** Last day kept, `YYYY-MM-DD`, the whole day included. */
  to?: string
}
