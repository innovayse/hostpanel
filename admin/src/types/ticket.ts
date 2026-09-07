/**
 * A support ticket, its list row and one reply in its thread.
 */

import type { Department } from './department'

/** DTO representing a support ticket reply. */
export interface TicketReply {
  /** Reply primary key. */
  id: number
  /** Reply message body. */
  message: string
  /** Display name of the reply author. */
  authorName: string
  /** Whether this reply was posted by a staff member. */
  isStaffReply: boolean
  /** ISO 8601 creation timestamp. */
  createdAt: string
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
  /** Current lifecycle status (Open, AwaitingReply, Answered, Closed). */
  status: string
  /** Priority level (Low, Medium, High). */
  priority: string
  /** Department FK, if assigned. */
  departmentId: number | null
  /** Department name, if assigned. */
  departmentName: string | null
  /** Assigned staff member FK, if any. */
  assignedToStaffId: number | null
  /** Assigned staff member name, if any. */
  assignedToStaffName: string | null
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
  /** Current lifecycle status as a string. */
  status: string
  /** Priority level as a string. */
  priority: string
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
  /** FK to the client who opened the ticket. */
  clientId: number
}
