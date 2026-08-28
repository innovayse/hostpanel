/** One message on a support ticket thread. */
export interface TicketReply {
  /** Reply primary key, used as the list key. */
  replyid: number | string
  /** Who wrote it. */
  name: string
  /** Staff name when a member of staff wrote it, empty when the client did. */
  admin: string
  /** When it was posted, already formatted by the backend for display. */
  date: string
  /** The reply body. */
  message: string
}
