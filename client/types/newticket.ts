/** The fields `POST /api/portal/client/tickets` needs to open a support ticket. */
export interface NewTicket {
  /** Department the ticket is routed to. */
  deptid: number | string
  /** Ticket subject line. */
  subject: string
  /** First message on the ticket. */
  message: string
  /** Priority the client chose, e.g. "Medium". */
  priority: string
}
