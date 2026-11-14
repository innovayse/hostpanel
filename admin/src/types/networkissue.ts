/**
 * A network issue, its payload and filter, plus the closed value sets the API speaks for it
 * (type, priority, status).
 */

/** Kind of network issue. */
export enum NetworkIssueType {
  /** Server-related issue. */
  Server = 'Server',
  /** Any other kind of issue. */
  Other = 'Other',
}

/** Priority of a network issue. */
export enum NetworkIssuePriority {
  /** Minimal impact. */
  Low = 'Low',
  /** Moderate impact. */
  Medium = 'Medium',
  /** Significant impact. */
  High = 'High',
  /** Service-threatening impact. */
  Critical = 'Critical',
}

/** Lifecycle status of a network issue. */
export enum NetworkIssueStatus {
  /** Reported, not yet looked at. */
  Reported = 'Reported',
  /** Being investigated. */
  Investigating = 'Investigating',
  /** Maintenance or fix scheduled. */
  Scheduled = 'Scheduled',
  /** Resolved. */
  Resolved = 'Resolved',
}

/** DTO representing a network issue. */
export interface NetworkIssue {
  /** Network issue primary key. */
  id: number
  /** Issue title. */
  title: string
  /** Issue type. */
  type: NetworkIssueType
  /** Server name, if applicable. */
  server: string | null
  /** Priority level. */
  priority: NetworkIssuePriority
  /** Lifecycle status. */
  status: NetworkIssueStatus
  /** ISO 8601 start date. */
  startDate: string
  /** ISO 8601 end date, if any. */
  endDate: string | null
  /** Description body. */
  description: string
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** Body that creates or replaces a network issue. */
export interface NetworkIssuePayload {
  /** Issue title. */
  title: string
  /** Issue type. */
  type: NetworkIssueType
  /** Server name, if applicable. */
  server: string | null
  /** Priority level. */
  priority: NetworkIssuePriority
  /** Lifecycle status. */
  status: NetworkIssueStatus
  /** ISO 8601 start date. */
  startDate: string
  /** ISO 8601 end date, if any. */
  endDate: string | null
  /** Description body. */
  description: string
}

/** What narrows the network issue list: the search text and the filters; each member left out means no narrowing. */
export interface NetworkIssueListFilters {
  /** Search term over the title, the server and the description; empty for none. */
  search?: string
  /** Keep only issues in this status. */
  status?: NetworkIssueStatus
  /** Keep only issues of this type. */
  type?: NetworkIssueType
  /** Keep only issues of this priority. */
  priority?: NetworkIssuePriority
}

/** Filter and paging of the network issue list. */
export interface NetworkIssueFilter extends NetworkIssueListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}
