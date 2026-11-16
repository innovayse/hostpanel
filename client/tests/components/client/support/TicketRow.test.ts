/**
 * Unit tests for TicketRow: the link to the ticket and the subject, department and status shown.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import TicketRow from '~/components/client/support/TicketRow.vue'
import type { ClientTicket } from '~/types/clientticket'
import { TicketPriority } from '~/types/ticketpriority'
import { TicketStatus } from '~/types/ticketstatus'
import { clientTicketPath } from '~/constants/clientRoutes'

/**
 * Builds a ticket with the fields the row reads.
 *
 * @param overrides - Fields to set.
 * @returns A ticket.
 */
const ticket = (overrides: Partial<ClientTicket> = {}): ClientTicket => ({
  id: 12, subject: 'Site is down', departmentName: 'Support', createdAt: '2026-01-02T10:00:00Z', lastReplyAt: null,
  priority: TicketPriority.High, status: TicketStatus.Open, replyCount: 0,
  ...overrides
})

describe('TicketRow', () => {
  it('links the whole row to the ticket', () => {
    const wrapper = mountClient(TicketRow, { props: { ticket: ticket() } })

    expect(wrapper.attributes('href')).toBe(clientTicketPath(12))
    expect(wrapper.attributes('data-testid')).toBe('ticket-row-12')
  })

  it('shows the subject, the department and the priority', () => {
    const wrapper = mountClient(TicketRow, { props: { ticket: ticket() } })

    expect(wrapper.text()).toContain('Site is down')
    expect(wrapper.text()).toContain('Support')
    expect(wrapper.text()).toContain('High')
  })

  it('shows the last reply only when there is one', () => {
    const without = mountClient(TicketRow, { props: { ticket: ticket() } })
    const withReply = mountClient(TicketRow, { props: { ticket: ticket({ lastReplyAt: '2026-01-03T10:00:00Z' }) } })

    expect(without.text()).not.toContain('client.tickets.lastReply')
    expect(withReply.text()).toContain('client.tickets.lastReply')
  })
})
