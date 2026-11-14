/**
 * A support ticket, its list row and one reply in its thread, plus the closed value sets the
 * API speaks for tickets (status, priority, bulk action, flag filter).
 */

/** Lifecycle status of a ticket, as the API names it. */
export enum TicketStatus {
  /** Open and awaiting staff response. */
  Open = 'Open',
  /** Staff replied, awaiting the client. */
  AwaitingReply = 'AwaitingReply',
  /** Closed. */
  Closed = 'Closed',
}

/** Priority label of a ticket, as the API names it. */
export enum TicketPriority {
  /** Not urgent. */
  Low = 'Low',
  /** Standard. */
  Medium = 'Medium',
  /** Urgent or critical. */
  High = 'High',
}

/** One action the tickets list can run on several tickets at once. */
export enum TicketBulkAction {
  /** Close the tickets. */
  Close = 'close',
  /** Delete the tickets permanently. */
  Delete = 'delete',
  /** Flag the tickets. */
  Flag = 'flag',
  /** Remove the flag from the tickets. */
  Unflag = 'unflag',
}

/** The values of the flag filter of the tickets list: the one choice besides "any" is the tickets staff flagged. */
export enum TicketFlagFilter {
  /** Only flagged tickets. */
  Flagged = 'flagged',
}

/** DTO representing a support ticket reply. */
export interface TicketReply {
  /** Reply primary key. */
  id: number
  /** Reply message body. */
  message: string
  /** Display name of the reply author; an empty string means the reply has no author. */
  authorName: string
  /** Whether this reply was posted by a staff member. */
  isStaffReply: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
}

/** The department a ticket is filed under. */
export interface TicketDepartment {
  /** Department FK, null when unassigned. */
  id: number | null
  /** Department name, null when unassigned. */
  name: string | null
}

/** The staff member a ticket is assigned to. */
export interface TicketAssignee {
  /** Staff member FK, null when unassigned. */
  id: number | null
  /** Staff member name, null when unassigned. */
  name: string | null
}

/** Full DTO representing a single support ticket with all its replies. */
export interface Ticket {
  /** Unique ticket identifier. */
  id: number
  /** Associated client identifier. */
  clientId: number
  /** Ticket subject line. */
  subject: string
  /** Initial message body. */
  message: string
  /** Current lifecycle status. */
  status: TicketStatus
  /** Priority level. */
  priority: TicketPriority
  /** The department the ticket is filed under. */
  department: TicketDepartment
  /** The staff member the ticket is assigned to. */
  assignee: TicketAssignee
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** All replies on this ticket. */
  replies: TicketReply[]
}

/** Summary DTO for ticket list views. */
export interface TicketListItem {
  /** Ticket primary key. */
  id: number
  /** Ticket subject line. */
  subject: string
  /** Current lifecycle status. */
  status: TicketStatus
  /** Priority level. */
  priority: TicketPriority
  /** ISO 8601 creation timestamp. */
  createdAt: string
  /** Total number of replies. */
  replyCount: number
  /** Department name, if assigned. */
  departmentName: string | null
  /** ISO 8601 timestamp of the most recent reply, if any. */
  lastReplyAt: string | null
  /** Whether this ticket has been flagged by staff. */
  isFlagged: boolean
  /** Full name of the client who opened the ticket; every ticket has one, since its client cannot be deleted. */
  clientName: string
  /** FK to the client who opened the ticket. */
  clientId: number
}

/** What narrows a client's ticket list: the search text and the filters of the filter dialog; each member left out means no narrowing. */
export interface ClientTicketListFilters {
  /** Subject search term; empty for none. */
  search?: string
  /** Keep only the tickets in this status. */
  status?: TicketStatus
  /** Keep only the tickets of this priority. */
  priority?: TicketPriority
  /** Keep only the tickets filed under this department. */
  departmentId?: number
}

/** What narrows the staff ticket list: everything that narrows a client's list, and the flag. */
export interface TicketListFilters extends ClientTicketListFilters {
  /** Keep only the tickets staff flagged. */
  flagged?: boolean
}

/** Filter and paging of the staff ticket list. */
export interface TicketFilter extends TicketListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}

/** Filter and paging of a client's ticket list. */
export interface ClientTicketFilter extends ClientTicketListFilters {
  /** Page, 1-based. */
  page: number
  /** Items per page. */
  pageSize: number
}

/** Body of a ticket opened by staff on behalf of a client. */
export interface CreateTicketPayload {
  /** Client the ticket is opened for. */
  clientId: number
  /** Subject line. */
  subject: string
  /** First message. */
  message: string
  /** Department that receives the ticket; `null` when none is chosen, which the API refuses. */
  departmentId: number | null
  /** Priority label. */
  priority: TicketPriority
}

/** Body of a ticket metadata change; only the members present are changed. */
export interface UpdateTicketPayload {
  /** New lifecycle status. */
  status?: TicketStatus
  /** New priority label. */
  priority?: TicketPriority
  /** New department. */
  departmentId?: number
  /** Staff member the ticket is assigned to. */
  assignedToStaffId?: number
}

/** Body of a staff reply to a ticket; the author is the signed-in staff member, resolved server-side. */
export interface TicketReplyPayload {
  /** Reply body. */
  message: string
}

/** Body of a bulk action on tickets. */
export interface TicketBulkPayload {
  /** Tickets the action applies to. */
  ticketIds: number[]
  /** The action to run on them. */
  action: TicketBulkAction
}
