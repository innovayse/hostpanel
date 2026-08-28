import type { TicketReply } from '~/types/ticketreply'

/**
 * One support ticket with its whole conversation, as
 * `GET /api/portal/client/tickets/:id` returns it.
 *
 * The replies arrive wrapped in a second object rather than as a bare array — that is the
 * WHMCS-shaped envelope the backend passes through, and it is kept rather than flattened so
 * the type matches what is actually on the wire.
 */
export interface TicketThread {
  /** Ticket subject line. */
  subject: string
  /** Current status, e.g. "Open", "Answered", "Closed". */
  status: string
  /** Priority, e.g. "Medium". */
  priority: string
  /** Department handling the ticket. */
  deptname: string
  /** When the ticket was opened, already formatted by the backend for display. */
  date: string
  /** The opening message. */
  message: string
  /** The reply thread, absent on a ticket nobody has answered yet. */
  replies?: {
    /** The replies themselves, oldest first. */
    reply: TicketReply[]
  }
}
