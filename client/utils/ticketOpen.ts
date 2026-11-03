/**
 * Whether a support ticket is still open.
 *
 * One rule for the dashboard's Open Tickets tile and the Open tab of the tickets page: a ticket
 * is open until it is closed, whether it waits for staff, for the client or for a reply.
 *
 * @module utils/ticketOpen
 */

import type { ClientTicket } from '~/types/clientticket'
import { TicketStatus } from '~/types/ticketstatus'

/**
 * Whether the ticket is anything but closed.
 *
 * @param ticket - The ticket to check.
 * @returns True for Open, AwaitingReply and Answered; false for Closed.
 */
export const isTicketOpen = (ticket: Pick<ClientTicket, 'status'>): boolean => ticket.status !== TicketStatus.Closed
