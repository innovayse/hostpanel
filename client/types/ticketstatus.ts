/**
 * Statuses a support ticket can be in, as the backend's `TicketStatus` names them on `ClientTicket.status`.
 *
 * Only the states the portal branches on are listed; the API may send others, which the
 * status badge shows verbatim.
 */
export enum TicketStatus {
  /** Waiting for staff. */
  Open = 'Open',
  /** The client has replied and staff owe an answer. */
  AwaitingReply = 'AwaitingReply',
  /** Finished; no further replies are accepted. */
  Closed = 'Closed'
}
