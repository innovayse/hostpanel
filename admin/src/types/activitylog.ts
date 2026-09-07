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
