/** One support ticket on the client's account, as `GET /api/portal/client/tickets` returns it. */
export interface ClientTicket {
  /** Ticket primary key. */
  id: number
  /** Public ticket reference shown to the client, e.g. "#123456". */
  tid: string
  /** FK to the department handling the ticket. */
  deptid: number
  /** Department display name. */
  deptname: string
  /** FK to the client who opened the ticket. */
  userid: number
  /** Name the ticket was opened under. */
  name: string
  /** Email the ticket was opened under. */
  email: string
  /** Addresses copied on every reply, comma-separated. */
  cc: string
  /** Ticket access key, used for the unauthenticated view link. */
  c: string
  /** When the ticket was opened. */
  date: string
  /** Ticket subject line. */
  subject: string
  /** Current status, e.g. "Open", "Answered", "Closed". */
  status: string
  /** Priority the client or staff set, e.g. "Medium". */
  urgency: string
  /** When the last reply was posted. */
  lastreply: string
  /** Staff flag marker; 0 when unflagged. */
  flag: number
  /** Service the ticket was raised against, empty when none. */
  service: string
}
