/** DTO representing a network issue. */
export interface NetworkIssue {
  /** Network issue primary key. */
  id: number
  /** Issue title. */
  title: string
  /** Issue type (Server, Other). */
  type: string
  /** Server name, if applicable. */
  server: string | null
  /** Priority level (Low, Medium, High, Critical). */
  priority: string
  /** Lifecycle status (Reported, Investigating, Scheduled, Resolved). */
  status: string
  /** ISO 8601 start date. */
  startDate: string
  /** ISO 8601 end date, if any. */
  endDate: string | null
  /** HTML description body. */
  description: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
}
