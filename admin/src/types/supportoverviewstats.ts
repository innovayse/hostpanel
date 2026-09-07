/** Support overview dashboard statistics. */
export interface SupportOverviewStats {
  /** Number of new tickets in the period. */
  newTickets: number
  /** Number of client replies in the period. */
  clientReplies: number
  /** Number of staff replies in the period. */
  staffReplies: number
  /** Number of tickets with no staff reply in the period. */
  ticketsWithoutReply: number
  /** Average time to first staff response, or null when no data. */
  averageFirstResponse: string | null
  /** Array of 24 integers representing ticket counts per hour (0-23). */
  ticketsByHour: number[]
}
